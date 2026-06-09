using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Web.Models.Common;
using Web.Models.Notifications;

namespace Web.Services;

public class NotificationClientService : INotificationClientService
{
    private const string TokenKey = "authToken";

    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationClientService> _logger;

    private HubConnection? _connection;
    private bool _starting;

    public event Func<NotificationDto, Task>? NotificationReceived;
    public event Func<int, Task>? UnreadCountChanged;

    public int UnreadCount { get; private set; }
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public NotificationClientService(
        HttpClient http,
        ILocalStorageService localStorage,
        IConfiguration configuration,
        ILogger<NotificationClientService> logger)
    {
        _http = http;
        _localStorage = localStorage;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        if (_connection != null || _starting) return;
        _starting = true;
        try
        {
            var token = await _localStorage.GetItemAsync<string>(TokenKey);
            if (string.IsNullOrEmpty(token))
            {
                // Not signed in; nothing to start.
                return;
            }

            var apiBase = _configuration["ApiBaseUrl"];
            if (string.IsNullOrWhiteSpace(apiBase))
            {
                _logger.LogWarning("ApiBaseUrl is not configured; skipping SignalR connection.");
                return;
            }

            var hubUrl = new Uri(new Uri(apiBase!), "/hubs/notifications").ToString();

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                        await _localStorage.GetItemAsync<string>(TokenKey);
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<JsonElement>("ReceiveNotification", async payload =>
            {
                try
                {
                    var dto = payload.Deserialize<NotificationDto>(new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (dto != null && NotificationReceived != null)
                    {
                        await NotificationReceived.Invoke(dto);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle ReceiveNotification");
                }
            });

            _connection.On<int>("UnreadCountChanged", async count =>
            {
                UnreadCount = count;
                if (UnreadCountChanged != null)
                {
                    await UnreadCountChanged.Invoke(count);
                }
            });

            _connection.Reconnected += async _ =>
            {
                await RefreshUnreadCountAsync();
            };

            await _connection.StartAsync();

            await RefreshUnreadCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start notifications connection");
        }
        finally
        {
            _starting = false;
        }
    }

    public async Task StopAsync()
    {
        if (_connection == null) return;
        try
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop notifications connection");
        }
        finally
        {
            _connection = null;
        }
    }

    public async Task<PaginatedList<NotificationDto>> GetAsync(int pageNumber, int pageSize, bool unreadOnly = false)
    {
        try
        {
            var url = $"api/notifications?pageNumber={pageNumber}&pageSize={pageSize}&unreadOnly={unreadOnly.ToString().ToLowerInvariant()}";
            var result = await _http.GetFromJsonAsync<PaginatedList<NotificationDto>>(url);
            return result ?? new PaginatedList<NotificationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notifications");
            return new PaginatedList<NotificationDto>();
        }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            var doc = await _http.GetFromJsonAsync<JsonElement>("api/notifications/unread-count");
            if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("count", out var c))
            {
                UnreadCount = c.GetInt32();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch unread notifications count");
        }
        return UnreadCount;
    }

    public async Task<bool> MarkReadAsync(Guid id)
    {
        try
        {
            var resp = await _http.PostAsync($"api/notifications/{id}/read", content: null);
            if (resp.IsSuccessStatusCode)
            {
                await RefreshUnreadCountAsync();
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification {Id} read", id);
        }
        return false;
    }

    public async Task<int> MarkAllReadAsync()
    {
        try
        {
            var resp = await _http.PostAsync("api/notifications/read-all", content: null);
            if (resp.IsSuccessStatusCode)
            {
                var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
                var n = doc.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                await RefreshUnreadCountAsync();
                return n;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications read");
        }
        return 0;
    }

    private async Task RefreshUnreadCountAsync()
    {
        var count = await GetUnreadCountAsync();
        if (UnreadCountChanged != null)
        {
            await UnreadCountChanged.Invoke(count);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
