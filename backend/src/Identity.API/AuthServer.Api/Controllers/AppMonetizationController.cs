using System.Diagnostics;
using AuthServer.Api.DTOs;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers;

[ApiController]
[Route("api/internal/apps/{appId}/monetization")]
[Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
public sealed class AppMonetizationController : ControllerBase
{
    private readonly AppMonetizationCommitService _commit;

    public AppMonetizationController(AppMonetizationCommitService commit)
    {
        _commit = commit;
    }

    [HttpPost("commit")]
    public async Task<IActionResult> Commit(
        string appId,
        [FromBody] AppMonetizationCommitRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("appId", appId);

        var result = await _commit.CommitAsync(appId, request, correlationId, cancellationToken);
        if (result.Succeeded && result.Response != null)
        {
            return Ok(result.Response);
        }

        return result.FailureKind switch
        {
            AppMonetizationCommitFailureKind.InvalidRequest => BadRequest(new { error = "InvalidRequest", message = result.Error }),
            AppMonetizationCommitFailureKind.InvalidSpec => BadRequest(new { error = "InvalidSpec", message = result.Error, errors = result.Errors }),
            AppMonetizationCommitFailureKind.SpecHashMismatch => Conflict(new { error = "SpecHashMismatch", message = result.Error }),
            AppMonetizationCommitFailureKind.PolicyViolation => Conflict(new { error = "PolicyViolation", message = result.Error, errors = result.Errors }),
            AppMonetizationCommitFailureKind.Forbidden => Forbid(),
            AppMonetizationCommitFailureKind.NotFound => NotFound(new { error = "NotFound" }),
            AppMonetizationCommitFailureKind.StripeProvisioningFailed => StatusCode(502, new { error = "StripeProvisioningFailed", message = result.Error }),
            _ => StatusCode(500, new { error = "MonetizationCommitFailed", message = result.Error ?? "Unknown error" })
        };
    }

    private string GetOrCreateCorrelationId()
    {
        var header = Request.Headers["x-correlation-id"].ToString();
        return string.IsNullOrWhiteSpace(header) ? Guid.NewGuid().ToString() : header;
    }
}
