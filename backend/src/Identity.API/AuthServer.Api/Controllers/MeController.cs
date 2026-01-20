using System.Diagnostics;
using System.Security.Claims;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [Route("api/me")]
    [ApiController]
    [Authorize]
    public class MeController : ControllerBase
    {
        private readonly MozaiksAppService _apps;
        private readonly ITeamMembersRepository _teamRepository;
        private readonly GovernanceApiClient _governanceApi;
        private readonly ICreatorDashboardService _creatorDashboard;
        private readonly IUserSettingsService _userSettings;
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public MeController(
            MozaiksAppService apps,
            ITeamMembersRepository teamRepository,
            GovernanceApiClient governanceApi,
            ICreatorDashboardService creatorDashboard,
            IUserSettingsService userSettings,
            StructuredLogEmitter logs,
            IUserContextAccessor userContextAccessor)
        {
            _apps = apps;
            _teamRepository = teamRepository;
            _governanceApi = governanceApi;
            _creatorDashboard = creatorDashboard;
            _userSettings = userSettings;
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<MeDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId
            };

            _logs.Info("Me.Dashboard.Requested", context);

            var errors = new List<DashboardErrorDto>();

            var ownedTask = _apps.GetByOwnerUserIdAsync(userId);
            var memberAppIdsTask = _teamRepository.GetAllAppIdsByUserIdAsync(userId);
            var username = GetCurrentUsername();
            var creatorDashboardTask = _creatorDashboard.GetDashboardAsync(userId, username, cancellationToken);

            await Task.WhenAll(ownedTask, memberAppIdsTask);

            var owned = ownedTask.Result ?? new List<MozaiksAppModel>();
            var memberIds = (memberAppIdsTask.Result ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            var member = memberIds.Count == 0
                ? new List<MozaiksAppModel>()
                : await _apps.GetByIdsAsync(memberIds);

            List<InvestorPositionDashboardDto> positions = new();
            List<InvestmentListItemDto> investments = new();

            try
            {
                positions = await _governanceApi.GetPositionsForUserAsync(userId, correlationId, cancellationToken);

                // Back-compat: Governance currently returns totalMp but not mpHeld/currentValue.
                // Derive mpHeld from totalMp only when mpHeld is unset.
                foreach (var p in positions)
                {
                    if (p is null)
                    {
                        continue;
                    }

                    if (p.MpHeld == 0 && p.TotalMp > 0)
                    {
                        p.MpHeld = p.TotalMp;
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new DashboardErrorDto { Code = "GovernancePositionsFetchFailed", Message = ex.Message });
                _logs.Error("Me.Dashboard.GovernancePositionsFailed", context, new { error = ex.Message, type = ex.GetType().Name });
            }

            try
            {
                investments = await _governanceApi.GetInvestmentsForUserAsync(userId, correlationId, cancellationToken);
            }
            catch (Exception ex)
            {
                errors.Add(new DashboardErrorDto { Code = "GovernanceInvestmentsFetchFailed", Message = ex.Message });
                _logs.Error("Me.Dashboard.GovernanceInvestmentsFailed", context, new { error = ex.Message, type = ex.GetType().Name });
            }

            CreatorDashboardResponse creatorDashboard;
            try
            {
                creatorDashboard = await creatorDashboardTask;
            }
            catch (Exception ex)
            {
                errors.Add(new DashboardErrorDto { Code = "CreatorDashboardFetchFailed", Message = ex.Message });
                _logs.Error("Me.Dashboard.CreatorDashboardFailed", context, new { error = ex.Message, type = ex.GetType().Name });

                creatorDashboard = new CreatorDashboardResponse
                {
                    UserId = userId,
                    Username = username,
                    Summary = new CreatorDashboardSummary
                    {
                        TotalApps = 0,
                        AppsByStatus = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["running"] = 0,
                            ["paused"] = 0,
                            ["stopped"] = 0,
                            ["failed"] = 0
                        },
                        TotalEvents24h = 0,
                        TotalUsers = 0,
                        ActiveUsers24h = 0,
                        TotalErrors24h = 0,
                        ErrorRate = 0d
                    },
                    Apps = new List<CreatorDashboardAppItem>(),
                    RecentActivity = new List<CreatorDashboardActivityItem>(),
                    Alerts = new List<CreatorDashboardAlertItem>()
                };
            }

            var response = new MeDashboardResponse
            {
                CorrelationId = correlationId,
                UserId = userId,
                Username = username,
                OwnedMozaiks = owned.Select(ToSummary).ToList(),
                MemberMozaiks = member.Select(ToSummary).ToList(),
                InvestorPositions = positions,
                Investments = investments,
                Errors = errors,
                Summary = creatorDashboard.Summary,
                Apps = creatorDashboard.Apps,
                RecentActivity = creatorDashboard.RecentActivity,
                Alerts = creatorDashboard.Alerts
            };

            _logs.Info("Me.Dashboard.Completed", context, new
            {
                ownedCount = response.OwnedMozaiks.Count(),
                memberCount = response.MemberMozaiks.Count(),
                positionsCount = response.InvestorPositions.Count(),
                investmentsCount = response.Investments.Count(),
                errorCount = response.Errors.Count()
            });

            return Ok(response);
        }

        [HttpGet("creator-dashboard")]
        public async Task<ActionResult<CreatorDashboardResponse>> GetCreatorDashboard(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId
            };

            _logs.Info("Me.CreatorDashboard.Requested", context);

            try
            {
                var dashboard = await _creatorDashboard.GetDashboardAsync(userId, GetCurrentUsername(), cancellationToken);

                _logs.Info("Me.CreatorDashboard.Completed", context, new
                {
                    totalApps = dashboard.Summary.TotalApps,
                    totalEvents24h = dashboard.Summary.TotalEvents24h
                });

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logs.Error("Me.CreatorDashboard.Failed", context, new { error = ex.Message, type = ex.GetType().Name });
                return StatusCode(500, new { error = "CreatorDashboardFailed", message = ex.Message });
            }
        }

        [HttpGet("settings")]
        public async Task<ActionResult<MeSettingsResponse>> GetMySettings(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId
            };

            _logs.Info("Me.Settings.Get.Requested", context);

            var settings = await _userSettings.GetAsync(userId, cancellationToken);

            _logs.Info("Me.Settings.Get.Completed", context);

            return Ok(settings);
        }

        [HttpPut("settings")]
        public async Task<ActionResult<MeSettingsResponse>> UpdateMySettings([FromBody] MeSettingsUpdateRequest request, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId
            };

            _logs.Info("Me.Settings.Put.Requested", context);

            var (isValid, errorMessage, settings) = await _userSettings.UpdateAsync(userId, request, cancellationToken);
            if (!isValid || settings is null)
            {
                _logs.Warn("Me.Settings.Put.ValidationFailed", context, new { error = errorMessage });
                return BadRequest(new { error = "invalid_settings", message = errorMessage ?? "Invalid settings." });
            }

            _logs.Info("Me.Settings.Put.Completed", context);

            return Ok(settings);
        }

        private string GetCurrentUserId()
        {
            return _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
        }

        private string? GetCurrentUsername()
        {
            return _userContextAccessor.GetUser(User)?.DisplayName ?? User.Identity?.Name;
        }

        private string GetOrCreateCorrelationId()
        {
            var header = Request.Headers["x-correlation-id"].ToString();
            return string.IsNullOrWhiteSpace(header) ? Guid.NewGuid().ToString() : header;
        }

        private MozaikSummaryDto ToSummary(MozaiksAppModel app)
        {
            var logo = app.LogoUrl;
            if (!string.IsNullOrWhiteSpace(logo) && !logo.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                logo = $"{Request.Scheme}://{Request.Host}/images/{logo}";
            }

            return new MozaikSummaryDto
            {
                Id = app.Id ?? string.Empty,
                Name = app.Name,
                LogoUrl = logo,
                Industry = app.Industry,
                Visibility = app.IsPublicMozaik == true ? "PUBLIC" : "PRIVATE",
                OwnerUserId = app.OwnerUserId,
                IsActive = app.IsActive
            };
        }
    }
}
