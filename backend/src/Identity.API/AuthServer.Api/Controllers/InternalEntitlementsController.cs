using AuthServer.Api.DTOs;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers;

[ApiController]
[Route("api/internal/entitlements")]
[Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
public sealed class InternalEntitlementsController : ControllerBase
{
    private readonly AppEntitlementsService _entitlements;

    public InternalEntitlementsController(
        AppEntitlementsService entitlements)
    {
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<ActionResult<EntitlementsResponse>> Get(
        [FromQuery] string userId,
        [FromQuery] string appId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "appId is required" });
        }

        var correlationId = HttpContext.TraceIdentifier;
        var snapshot = await _entitlements.GetSnapshotAsync(userId.Trim(), appId.Trim(), correlationId, cancellationToken);
        var billingSource = snapshot.AppStatus.IsActive ? snapshot.AppStatus : snapshot.PlatformStatus;

        var response = new EntitlementsResponse
        {
            AllowHosting = snapshot.Entitlements.AllowHosting,
            AllowExportRepo = snapshot.Entitlements.AllowExportRepo,
            AllowWorkerMode = snapshot.Entitlements.AllowWorkerMode,
            HostingLevel = snapshot.Entitlements.HostingLevel,
            AppPlanId = snapshot.Entitlements.AppPlanId,
            PlatformPlanId = snapshot.Entitlements.PlatformPlanId,
            Domains = new EntitlementDomains
            {
                Max = snapshot.Entitlements.DomainsMax,
                Used = null
            },
            Email = new EntitlementEmail
            {
                Enabled = snapshot.Entitlements.EmailEnabled,
                MaxDomains = snapshot.Entitlements.EmailMaxDomains
            },
            Tokens = new EntitlementTokens
            {
                MonthlyLimit = snapshot.Entitlements.TokensMonthlyLimit
            },
            Hosting = new EntitlementHosting
            {
                Tier = snapshot.Entitlements.HostingLevel
            },
            Fees = new EntitlementFees
            {
                TransactionFeeBps = snapshot.Entitlements.TransactionFeeBps
            },
            Billing = new EntitlementBilling
            {
                StripeSubscriptionId = billingSource.SubscriptionId,
                CurrentPeriodEndUtc = billingSource.CurrentPeriodEndUtc ?? billingSource.ExpiresAtUtc
            }
        };

        return Ok(response);
    }
}
