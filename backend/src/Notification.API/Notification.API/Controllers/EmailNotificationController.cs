using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;
using Notification.API.Enums;
using Notification.API.Models;
using Notification.API.Services;

namespace Notification.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailNotificationController : ControllerBase
    {
        private readonly INotificationService _service;

        public EmailNotificationController(INotificationService service)
        {
            _service = service;
        }

        [HttpPost("SendEmailNotification")]
        [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
        public async Task<IActionResult> SendEmail([FromBody] NotificationModel notification)
        {
            var createdNotification = await _service.CreateAsync(notification, NotificationType.Email);
            return CreatedAtAction(nameof(GetById), new { id = createdNotification.Id }, createdNotification);
        }

        [HttpGet("GetEmailNotificationById/{id}")]
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

        [HttpGet("GetEmailNotifications/{userId}/{appId}")]
        [Authorize]
        public async Task<IActionResult> GetAll(string userId,string appId)
        {
            var notifications = await _service.GetAllUserNotificationAsync(userId,appId); 
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
            //await _service.DeleteAsync(id);
            return NoContent();
        }
    }
}
