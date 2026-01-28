using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Mozaiks.Auth;

namespace Notification.API;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly IUserContextAccessor _userContextAccessor;

    public NotificationHub(IUserContextAccessor userContextAccessor)
    {
        _userContextAccessor = userContextAccessor;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string? GetUserId()
        => _userContextAccessor.GetUser(Context.User)?.UserId;

    private static string UserGroup(string userId) => $"user:{userId}";
}
