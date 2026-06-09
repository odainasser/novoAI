using Web.Models.Common;
using Web.Models.Notifications;

namespace Web.Services;

public interface INotificationClientService : IAsyncDisposable
{
    event Func<NotificationDto, Task>? NotificationReceived;
    event Func<int, Task>? UnreadCountChanged;

    int UnreadCount { get; }
    bool IsConnected { get; }

    Task StartAsync();
    Task StopAsync();
    Task<PaginatedList<NotificationDto>> GetAsync(int pageNumber, int pageSize, bool unreadOnly = false);
    Task<int> GetUnreadCountAsync();
    Task<bool> MarkReadAsync(Guid id);
    Task<int> MarkAllReadAsync();
}
