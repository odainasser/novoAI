using Application.Common.Models;
using Application.Features.Notifications;
using Domain.Enums;

namespace Application.Common.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Persist a notification for a single user and push it in real-time.
    /// </summary>
    Task SendAsync(
        Guid userId,
        NotificationType type,
        string titleEn,
        string titleAr,
        string bodyEn,
        string bodyAr,
        string? link = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist the same notification for many users and push it in real-time.
    /// </summary>
    Task SendBulkAsync(
        IEnumerable<Guid> userIds,
        NotificationType type,
        string titleEn,
        string titleAr,
        string bodyEn,
        string bodyAr,
        string? link = null,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<NotificationDto>> GetForCurrentUserAsync(
        int pageNumber,
        int pageSize,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountForCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default);
}
