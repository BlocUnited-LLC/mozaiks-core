using Insights.API.DTOs;
using Insights.API.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace Insights.API.Controllers;

[ApiController]
[Route("api/insights/apps")]
[Authorize]
public sealed class SdkStatusController : ControllerBase
{
    private readonly IMozaiksAppReadRepository _apps;
    private readonly ITeamMemberReadRepository _members;
    private readonly IApiKeyUsageRepository _usage;
    private readonly IEventRepository _events;
    private readonly IUserContextAccessor _userContextAccessor;

    public SdkStatusController(
        IMozaiksAppReadRepository apps,
        ITeamMemberReadRepository members,
        IApiKeyUsageRepository usage,
        IEventRepository events,
        IUserContextAccessor userContextAccessor)
    {
        _apps = apps;
        _members = members;
        _usage = usage;
        _events = events;
        _userContextAccessor = userContextAccessor;
    }

    [HttpGet("{appId}/sdk-status")]
    public async Task<ActionResult<SdkStatusResponse>> GetSdkStatus(string appId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        var app = await _apps.GetByIdAsync(appId, cancellationToken);
        if (app is null)
        {
            return NotFound(new { error = "NotFound" });
        }

        if (!IsPlatformAdmin())
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var isOwner = string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase);
            var isMember = isOwner || await _members.IsMemberAsync(appId, userId, cancellationToken);

            if (!isMember)
            {
                return Forbid();
            }
        }

        var usage = await _usage.GetByAppIdAsync(appId, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var eventsLast24hRaw = await _events.CountSinceAsync(appId, nowUtc.AddHours(-24), cancellationToken);
        var eventsLast24h = eventsLast24hRaw >= int.MaxValue ? int.MaxValue : (int)eventsLast24hRaw;

        if (eventsLast24h > 0 || usage is not null)
        {
            await _usage.SetEventsLast24hAsync(appId, eventsLast24h, nowUtc, cancellationToken);
        }

        var connected = eventsLast24h > 0
                        || usage?.LastPingAt is not null
                        || usage?.LastKpiPushAt is not null
                        || usage?.LastEventPushAt is not null;

        return Ok(new SdkStatusResponse(
            Connected: connected,
            LastPingAt: usage?.LastPingAt,
            LastKpiPushAt: usage?.LastKpiPushAt,
            LastEventPushAt: usage?.LastEventPushAt,
            EventsLast24h: eventsLast24h,
            SdkVersion: usage?.SdkVersion));
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

    private string GetCurrentUserId()
        => _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
}
