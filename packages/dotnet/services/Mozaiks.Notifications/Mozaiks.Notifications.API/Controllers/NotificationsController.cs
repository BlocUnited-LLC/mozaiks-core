using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.API.Enums;
using Notification.API.Services;
using Mozaiks.Auth;

namespace Notification.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;
    private readonly IUserContextAccessor _userContextAccessor;

    public NotificationsController(INotificationService service, IUserContextAccessor userContextAccessor)
    {
        _service = service;
        _userContextAccessor = userContextAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest("appId is required.");
        }

        var userId = _userContextAccessor.GetUser(User)?.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var notifications = await _service.GetAllUserNotificationAsync(userId, appId);
        return Ok(notifications);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var userId = _userContextAccessor.GetUser(User)?.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var notification = await _service.GetByIdAsync(id);
        if (notification is null || notification.RecepientId != userId)
        {
            return NotFound();
        }

        if (notification.Status == NotificationStatus.Read)
        {
            return NoContent();
        }

        notification.Status = NotificationStatus.Read;
        notification.UpdatedAt = DateTime.UtcNow;
        await _service.UpdateAsync(notification);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = _userContextAccessor.GetUser(User)?.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var notification = await _service.GetByIdAsync(id);
        if (notification is null || notification.RecepientId != userId)
        {
            return NotFound();
        }

        notification.Status = NotificationStatus.Deleted;
        notification.UpdatedAt = DateTime.UtcNow;
        await _service.UpdateAsync(notification);

        return NoContent();
    }
}
