using AuthServer.Api.Models;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Text;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InviteController : ControllerBase
    {
        private readonly InviteService _inviteService;
        private readonly HttpClient _httpClient;
        private readonly IUserContextAccessor _userContextAccessor;

        public InviteController(InviteService inviteService, HttpClient httpClient, IUserContextAccessor userContextAccessor)
        {
            _inviteService = inviteService;
            _httpClient = httpClient;
            _userContextAccessor = userContextAccessor;
        }
        [HttpPost("SendInvite")]
        public async Task<IActionResult> CreateInvite([FromBody] InviteModel inviteModel)
        {
            var callerUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(callerUserId) || callerUserId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (!IsPlatformAdmin())
            {
                if (!string.IsNullOrWhiteSpace(inviteModel.InvitedByUserId) &&
                    !string.Equals(inviteModel.InvitedByUserId, callerUserId, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                inviteModel.InvitedByUserId = callerUserId;
            }

            var invite = await _inviteService.CreateInviteAsync(inviteModel);
            if(invite != null)
            {
                var notification = new
                {
                    AppId = invite.AppId,
                    RecepientId = invite.ReceipentUserId,
                    Email = invite.SenderEmail,
                    Message = $"You have received an invite from {invite.InvitedByUserId}"
                };

                var content = new StringContent(JsonConvert.SerializeObject(notification), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://moz-notifications.azurewebsites.net/api/PushNotification/SendPushNotification", content);

                if (response.IsSuccessStatusCode)
                {
                    
                }
            }
            return CreatedAtAction(nameof(GetInviteById), new { id = invite.Id }, invite);
        }

        [HttpGet("GetInviteById/{id}")]
        public async Task<IActionResult> GetInviteById(string id)
        {
            var callerUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(callerUserId) || callerUserId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var invite = await _inviteService.GetInviteByIdAsync(id);
            if (invite == null)
            {
                return NotFound();
            }

            if (!IsPlatformAdmin() &&
                !string.Equals(invite.InvitedByUserId, callerUserId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(invite.ReceipentUserId, callerUserId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            return Ok(invite);
        }

        [HttpGet("GetSentInvites/{userId}")]
        public async Task<IActionResult> GetSentInvites(string userId)
        {
            var callerUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(callerUserId) || callerUserId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (!IsPlatformAdmin() && !string.Equals(userId, callerUserId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var invites = await _inviteService.GetSentInvitesAsync(userId);
            return Ok(invites);
        }

        [HttpGet("GetReceivedInvites/{userId}")]
        public async Task<IActionResult> GetReceivedInvites(string userId)
        {
            var callerUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(callerUserId) || callerUserId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (!IsPlatformAdmin() && !string.Equals(userId, callerUserId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var invites = await _inviteService.GetReceivedInvitesAsync(userId);
            return Ok(invites);
        }

        [HttpPut("UpdateInvite/{id}/{status}")]
        public async Task<IActionResult> UpdateInvite(string id, int status)
        {
            var callerUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(callerUserId) || callerUserId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var invite = await _inviteService.GetInviteByIdAsync(id);
            if (invite == null)
            {
                return NotFound();
            }

            if (!IsPlatformAdmin() &&
                !string.Equals(invite.ReceipentUserId, callerUserId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
            
            //await _inviteService.UpdateInviteAsync(invite);
            await _inviteService.UpdateInviteStatusAsync(id, status);
            
            return NoContent();
        }

        private string GetCurrentUserId()
        {
            return _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
        }

        private bool IsPlatformAdmin()
        {
            var user = _userContextAccessor.GetUser(User);
            if (user is null)
            {
                return false;
            }

            return user.IsSuperAdmin
                   || user.Roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
        }
    }
}
