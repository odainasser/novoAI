using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Infrastructure.Hubs;

[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNameForUser(userId));
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNameForUser(userId));
        }
        await base.OnDisconnectedAsync(exception);
    }

    public static string GroupNameForUser(Guid userId) => GroupNameForUser(userId.ToString());
    public static string GroupNameForUser(string userId) => $"user:{userId}";
}

public interface INotificationClient
{
    Task ReceiveNotification(object notification);
    Task UnreadCountChanged(int count);
}
