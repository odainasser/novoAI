using Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] bool? unreadOnly,
            [FromServices] INotificationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetForCurrentUserAsync(
                pageNumber ?? 1,
                pageSize ?? 20,
                unreadOnly ?? false,
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetNotifications")
        .WithSummary("Get current user's notifications");

        group.MapGet("/unread-count", async (
            [FromServices] INotificationService service,
            CancellationToken cancellationToken) =>
        {
            var count = await service.GetUnreadCountForCurrentUserAsync(cancellationToken);
            return Results.Ok(new { count });
        })
        .WithName("GetUnreadNotificationsCount")
        .WithSummary("Get current user's unread notification count");

        group.MapPost("/{id:guid}/read", async (
            Guid id,
            [FromServices] INotificationService service,
            CancellationToken cancellationToken) =>
        {
            var ok = await service.MarkReadAsync(id, cancellationToken);
            return ok ? Results.NoContent() : Results.NotFound();
        })
        .WithName("MarkNotificationRead")
        .WithSummary("Mark a notification as read");

        group.MapPost("/read-all", async (
            [FromServices] INotificationService service,
            CancellationToken cancellationToken) =>
        {
            var count = await service.MarkAllReadAsync(cancellationToken);
            return Results.Ok(new { count });
        })
        .WithName("MarkAllNotificationsRead")
        .WithSummary("Mark all of the current user's notifications as read");
    }
}
