using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Mozaiks.Auth;
using Notification.API.DTOs;
using Notification.API.Enums;
using Notification.API.Models;
using Notification.API.Services;

namespace Notification.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PushNotificationController : ControllerBase
    {
        private readonly INotificationService _service;
        private readonly IHubContext<NotificationHub> _hubContext;

        public PushNotificationController(INotificationService service, IHubContext<NotificationHub> hubContext)
        {
            _service = service;
            _hubContext = hubContext;
        }

        [HttpPost("SendPushNotification")]
        [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
        public async Task<IActionResult> SendNotification([FromBody] NotificationModel notification)
        {
            var createdNotification = await _service.CreateAsync(notification, NotificationType.Push);
            return CreatedAtAction(nameof(GetById), new { id = createdNotification.Id }, createdNotification);
        }

        [HttpPost("SendAppBuildEvent")]
        [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
        public async Task<IActionResult> SendAppBuildEvent([FromBody] AppBuildEventRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AppId) ||
                string.IsNullOrWhiteSpace(request.RecipientUserId) ||
                string.IsNullOrWhiteSpace(request.BuildId) ||
                string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new { error = "InvalidRequest" });
            }

            var status = request.Status.Trim().ToLowerInvariant();
            if (status != "built" && status != "error")
            {
                return BadRequest(new { error = "InvalidStatus", allowed = new[] { "built", "error" } });
            }

            var payload = new
            {
                type = "app.build",
                appId = request.AppId,
                buildId = request.BuildId,
                status
            };

            await _hubContext.Clients.Group($"user:{request.RecipientUserId}")
                .SendAsync("app.build", payload);

            var appLabel = string.IsNullOrWhiteSpace(request.AppName) ? request.AppId : request.AppName.Trim();
            var title = status == "built" ? "Build completed" : "Build failed";
            var message = status == "built"
                ? $"Build completed for {appLabel}."
                : $"Build failed for {appLabel}.";

            var notification = new NotificationModel
            {
                AppId = request.AppId,
                RecepientId = request.RecipientUserId,
                Subject = title,
                Message = message,
                Email = string.Empty
            };

            await _service.CreateAsync(notification, NotificationType.Push);

            return Accepted();
        }

        [HttpGet("GetPushNotificationById/{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(string id)
        {
            var notification = await _service.GetByIdAsync(id);
            if (notification == null)
            {
                return NotFound();
            }
            return Ok(notification);
        }

        [HttpGet("GetPushNotifications/{userId}/{appId}")]
        [Authorize]
        public async Task<IActionResult> GetAll(string userId, string appId)
        {
            var notifications = await _service.GetAllUserNotificationAsync(userId, appId);
            return Ok(notifications);
        }

        [HttpPut("UpdateStatus/{id}")]
        [Authorize]
        public async Task<IActionResult> Update(string id, [FromBody] NotificationModel notification)
        {
            if (id != notification.Id)
            {
                return BadRequest();
            }

            await _service.UpdateAsync(notification);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(string id)
        {
            var notification = await _service.GetByIdAsync(id);
            if (notification == null)
            {
                return NotFound();
            }

            notification.Status = NotificationStatus.Deleted;
            notification.UpdatedAt = DateTime.UtcNow;
            await _service.UpdateAsync(notification);
            return NoContent();
        }
    }
}
