using System.Diagnostics;
using System.Security.Claims;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [Route("api/me/security/2fa")]
    [ApiController]
    [Authorize]
    public class MeSecurityController : ControllerBase
    {
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public MeSecurityController(StructuredLogEmitter logs, IUserContextAccessor userContextAccessor)
        {
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpPost("setup")]
        public IActionResult Setup2Fa()
            => NotImplemented("Me.Security.2FA.Setup");

        [HttpPost("verify")]
        public IActionResult Verify2Fa()
            => NotImplemented("Me.Security.2FA.Verify");

        [HttpPost("disable")]
        public IActionResult Disable2Fa()
            => NotImplemented("Me.Security.2FA.Disable");

        private IActionResult NotImplemented(string message)
        {
            var userId = GetCurrentUserId();
            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId
            };

            _logs.Warn(message, context);

            return StatusCode(501, new { error = "not_implemented", message = "2FA is not implemented yet." });
        }

        private string GetCurrentUserId()
        {
            return _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
        }

        private string GetOrCreateCorrelationId()
        {
            var header = Request.Headers["x-correlation-id"].ToString();
            return string.IsNullOrWhiteSpace(header) ? Guid.NewGuid().ToString() : header;
        }
    }
}
