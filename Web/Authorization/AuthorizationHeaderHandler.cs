using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Web.Models.Auth;

namespace Web.Authorization;

public class AuthorizationHeaderHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigation;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string TokenKey = "authToken";
    private const string UserKey = "currentUser";
    private const string RefreshTokenKey = "refreshToken";
    private const string RetriedHeader = "X-Refresh-Retried";

    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static Task<string?>? _inFlightRefresh;
    private static int _redirectingOnUnauthorized;

    public AuthorizationHeaderHandler(
        ILocalStorageService localStorage,
        NavigationManager navigation,
        IHttpClientFactory httpClientFactory)
    {
        _localStorage = localStorage;
        _navigation = navigation;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await AttachAccessTokenAsync(request);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized || !ShouldAttemptRefreshFor(request))
        {
            return response;
        }

        if (request.Headers.Contains(RetriedHeader))
        {
            await HandleSessionExpiredAsync();
            return response;
        }

        var newAccessToken = await RefreshAccessTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(newAccessToken))
        {
            await HandleSessionExpiredAsync();
            return response;
        }

        response.Dispose();

        var retried = await CloneRequestAsync(request, newAccessToken);
        retried.Headers.Add(RetriedHeader, "1");
        return await base.SendAsync(retried, cancellationToken);
    }

    private async Task AttachAccessTokenAsync(HttpRequestMessage request)
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>(TokenKey);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch
        {
            // ignore failures reading token
        }
    }

    private static bool ShouldAttemptRefreshFor(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        // Don't try to refresh on the auth endpoints themselves —
        // login failures, the refresh call, and logout are all handled differently.
        return !path.Contains("/api/auth/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        // Single-flight: many parallel 401s share one refresh call.
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            _inFlightRefresh ??= DoRefreshAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }

        try
        {
            return await _inFlightRefresh;
        }
        finally
        {
            await _refreshLock.WaitAsync(CancellationToken.None);
            try
            {
                _inFlightRefresh = null;
            }
            finally
            {
                _refreshLock.Release();
            }
        }
    }

    private async Task<string?> DoRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            // Use a fresh HttpClient so we don't recurse through this handler
            using var client = _httpClientFactory.CreateClient("ApiClient");
            using var response = await client.PostAsJsonAsync(
                "api/auth/refresh",
                new { RefreshToken = refreshToken },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
            if (body is null || !body.Success || string.IsNullOrEmpty(body.Token))
                return null;

            await _localStorage.SetItemAsync(TokenKey, body.Token);
            if (!string.IsNullOrEmpty(body.RefreshToken))
            {
                await _localStorage.SetItemAsync(RefreshTokenKey, body.RefreshToken);
            }
            if (body.User is not null)
            {
                await _localStorage.SetItemAsync(UserKey, body.User);
            }

            return body.Token;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original, string newAccessToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version
        };

        if (original.Content is not null)
        {
            var ms = new MemoryStream();
            await original.Content.CopyToAsync(ms);
            ms.Position = 0;
            var content = new StreamContent(ms);
            foreach (var header in original.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            clone.Content = content;
        }

        foreach (var header in original.Headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                continue;
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
        return clone;
    }

    private async Task HandleSessionExpiredAsync()
    {
        if (Interlocked.Exchange(ref _redirectingOnUnauthorized, 1) == 1)
            return;

        try
        {
            try { await _localStorage.RemoveItemAsync(TokenKey); } catch { }
            try { await _localStorage.RemoveItemAsync(UserKey); } catch { }
            try { await _localStorage.RemoveItemAsync(RefreshTokenKey); } catch { }

            // We do NOT inject AuthenticationStateProvider here — it depends on IAuthenticationService,
            // which depends on the ApiClient HttpClient, whose pipeline contains this handler.
            // forceLoad: true reloads the whole app, so the auth state is rebuilt from cleared storage.

            var current = _navigation.ToBaseRelativePath(_navigation.Uri) ?? string.Empty;
            if (!current.StartsWith("login", StringComparison.OrdinalIgnoreCase) &&
                !current.StartsWith("logout", StringComparison.OrdinalIgnoreCase) &&
                !current.StartsWith("forgot-password", StringComparison.OrdinalIgnoreCase) &&
                !current.StartsWith("reset-password", StringComparison.OrdinalIgnoreCase))
            {
                _navigation.NavigateTo("/login?sessionExpired=1", forceLoad: true);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _redirectingOnUnauthorized, 0);
        }
    }
}
