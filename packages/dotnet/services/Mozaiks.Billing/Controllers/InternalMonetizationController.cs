using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;
using Payment.API.DTOs;
using Payment.API.Services;

namespace Payment.API.Controllers;

[ApiController]
[Route("api/internal/mozaiks/pay/monetization")]
[Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
public sealed class InternalMonetizationController : ControllerBase
{
    private readonly MozaiksPayService _mozaiksPay;

    public InternalMonetizationController(MozaiksPayService mozaiksPay)
    {
        _mozaiksPay = mozaiksPay;
    }

    [HttpPost("price")]
    public async Task<ActionResult<MonetizationPriceProvisionResponse>> ProvisionPrice(
        [FromBody] MonetizationPriceProvisionRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { error = "InvalidRequest" });
        }

        try
        {
            var result = await _mozaiksPay.ProvisionMonetizationPriceAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "InvalidRequest", message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "StripeProvisioningFailed", message = ex.Message });
        }
    }
}
