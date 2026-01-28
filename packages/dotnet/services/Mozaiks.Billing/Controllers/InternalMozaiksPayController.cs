using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;
using Payment.API.DTOs;
using Payment.API.Services;

namespace Payment.API.Controllers;

[ApiController]
[Route("api/internal/mozaiks/pay")]
[Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
public sealed class InternalMozaiksPayController : ControllerBase
{
    private readonly MozaiksPayService _mozaiksPay;

    public InternalMozaiksPayController(MozaiksPayService mozaiksPay)
    {
        _mozaiksPay = mozaiksPay;
    }

    [HttpGet("subscription-status")]
    public async Task<ActionResult<MozaiksPayStatusResponse>> GetSubscriptionStatus(
        [FromQuery] string userId,
        [FromQuery] string scope,
        [FromQuery] string? appId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        if (string.IsNullOrWhiteSpace(scope))
        {
            return BadRequest(new { error = "scope is required" });
        }

        var result = await _mozaiksPay.GetSubscriptionStatusAsync(userId.Trim(), scope.Trim(), appId, cancellationToken);
        return Ok(result);
    }
}

