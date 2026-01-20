using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.API.DTOs;
using Payment.API.Infrastructure.Observability;
using Payment.API.Services;
using Mozaiks.Auth;

namespace Payment.API.Controllers
{
    [ApiController]
    [Route("api/mozaiks/pay")]
    public class MozaiksPayController : ControllerBase
    {
        private readonly MozaiksPayService _mozaiksPay;
        private readonly StructuredLogEmitter _logs;
        private readonly AnalyticsEventEmitter _analytics;
        private readonly ICorrelationContextAccessor _correlation;
        private readonly IUserContextAccessor _userContextAccessor;

        public MozaiksPayController(
            MozaiksPayService mozaiksPay,
            StructuredLogEmitter logs,
            AnalyticsEventEmitter analytics,
            ICorrelationContextAccessor correlation,
            IUserContextAccessor userContextAccessor)
        {
            _mozaiksPay = mozaiksPay;
            _logs = logs;
            _analytics = analytics;
            _correlation = correlation;
            _userContextAccessor = userContextAccessor;
        }

        [HttpPost("checkout")]
        [Authorize]
        public async Task<ActionResult<MozaiksPayCheckoutResponse>> CreateCheckout([FromBody] MozaiksPayCheckoutRequest request, CancellationToken cancellationToken)
        {
            var userId = _userContextAccessor.GetUser(User)?.UserId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            var actor = new ActorContext { UserId = userId, AppId = request.AppId };
            var entity = new EntityContext { AppId = request.AppId };

            _logs.Info("MozaiksPay.Checkout.Started", actor, entity, new { _correlation.CorrelationId, request.Scope, request.Mode, request.PlanId });
            _analytics.Emit("server.mozaikspay.checkout_started", actor, entity, new { request.Scope, request.Mode, request.PlanId });

            var response = await _mozaiksPay.CreateCheckoutAsync(userId, request, cancellationToken);
            return Ok(response);
        }

        [HttpGet("subscription-status")]
        [Authorize]
        public async Task<ActionResult<MozaiksPayStatusResponse>> GetSubscriptionStatus([FromQuery] string scope, [FromQuery] string? appId, CancellationToken cancellationToken)
        {
            var userId = _userContextAccessor.GetUser(User)?.UserId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            var result = await _mozaiksPay.GetSubscriptionStatusAsync(userId, scope, appId, cancellationToken);
            return Ok(result);
        }

        [HttpPost("cancel")]
        [Authorize]
        public async Task<IActionResult> Cancel([FromBody] MozaiksPayCancelRequest request, CancellationToken cancellationToken)
        {
            var userId = _userContextAccessor.GetUser(User)?.UserId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            await _mozaiksPay.CancelAsync(userId, request, cancellationToken);
            return NoContent();
        }
    }
}
