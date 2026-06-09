using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Notifications;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Hubs;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub, INotificationClient> _hub;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        IHubContext<NotificationHub, INotificationClient> hub,
        ICurrentUserService currentUserService,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hub = hub;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task SendAsync(
        Guid userId,
        NotificationType type,
        string titleEn,
        string titleAr,
        string bodyEn,
        string bodyAr,
        string? link = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return;

        var entity = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            TitleEn = titleEn ?? string.Empty,
            TitleAr = titleAr ?? string.Empty,
            BodyEn = bodyEn ?? string.Empty,
            BodyAr = bodyAr ?? string.Empty,
            Link = link,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await PushAsync(userId, entity, cancellationToken);
    }

    public async Task SendBulkAsync(
        IEnumerable<Guid> userIds,
        NotificationType type,
        string titleEn,
        string titleAr,
        string bodyEn,
        string bodyAr,
        string? link = null,
        CancellationToken cancellationToken = default)
    {
        var distinct = (userIds ?? Enumerable.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinct.Count == 0) return;

        var now = DateTime.UtcNow;
        var entities = distinct.Select(uid => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = uid,
            Type = type,
            TitleEn = titleEn ?? string.Empty,
            TitleAr = titleAr ?? string.Empty,
            BodyEn = bodyEn ?? string.Empty,
            BodyAr = bodyAr ?? string.Empty,
            Link = link,
            IsRead = false,
            CreatedAt = now
        }).ToList();

        _context.Notifications.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var entity in entities)
        {
            await PushAsync(entity.UserId, entity, cancellationToken);
        }
    }

    public async Task<PaginatedList<NotificationDto>> GetForCurrentUserAsync(
        int pageNumber,
        int pageSize,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var (currentUserId, _) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId == Guid.Empty)
            return new PaginatedList<NotificationDto>(new List<NotificationDto>(), 0, pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Notifications.AsNoTracking()
            .Where(n => n.UserId == currentUserId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                TitleEn = n.TitleEn,
                TitleAr = n.TitleAr,
                BodyEn = n.BodyEn,
                BodyAr = n.BodyAr,
                Link = n.Link,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<NotificationDto>(items, total, pageNumber, pageSize);
    }

    public async Task<int> GetUnreadCountForCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var (currentUserId, _) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId == Guid.Empty) return 0;

        return await _context.Notifications
            .Where(n => n.UserId == currentUserId && !n.IsRead)
            .CountAsync(cancellationToken);
    }

    public async Task<bool> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var (currentUserId, _) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId == Guid.Empty) return false;

        var entity = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == currentUserId, cancellationToken);

        if (entity == null) return false;
        if (entity.IsRead) return true;

        entity.IsRead = true;
        entity.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await PushUnreadCountAsync(currentUserId, cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var (currentUserId, _) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId == Guid.Empty) return 0;

        var now = DateTime.UtcNow;
        var unread = await _context.Notifications
            .Where(n => n.UserId == currentUserId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0) return 0;

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        await _context.SaveChangesAsync(cancellationToken);

        await PushUnreadCountAsync(currentUserId, cancellationToken);
        return unread.Count;
    }

    private async Task PushAsync(Guid userId, Notification entity, CancellationToken cancellationToken)
    {
        try
        {
            var dto = new NotificationDto
            {
                Id = entity.Id,
                Type = entity.Type,
                TitleEn = entity.TitleEn,
                TitleAr = entity.TitleAr,
                BodyEn = entity.BodyEn,
                BodyAr = entity.BodyAr,
                Link = entity.Link,
                IsRead = entity.IsRead,
                CreatedAt = entity.CreatedAt,
                ReadAt = entity.ReadAt
            };

            await _hub.Clients
                .Group(NotificationHub.GroupNameForUser(userId))
                .ReceiveNotification(dto);

            await PushUnreadCountAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push notification {NotificationId} to user {UserId}", entity.Id, userId);
        }
    }

    private async Task PushUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync(cancellationToken);

            await _hub.Clients
                .Group(NotificationHub.GroupNameForUser(userId))
                .UnreadCountChanged(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push unread count to user {UserId}", userId);
        }
    }
}
