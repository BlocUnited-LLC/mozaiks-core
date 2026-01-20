using System.Diagnostics;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [Route("api/apps")]
    [ApiController]
    [Authorize]
    public class AppsController : ControllerBase
    {
        private readonly MozaiksAppService _apps;
        private readonly IAppLifecycleService _lifecycle;
        private readonly ITeamMembersRepository _teamRepository;
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public AppsController(
            MozaiksAppService apps,
            IAppLifecycleService lifecycle,
            ITeamMembersRepository teamRepository,
            StructuredLogEmitter logs,
            IUserContextAccessor userContextAccessor)
        {
            _apps = apps;
            _lifecycle = lifecycle;
            _teamRepository = teamRepository;
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpGet]
        public async Task<ActionResult<MyMozaiksAppsResponse>> GetMyApps(CancellationToken cancellationToken)
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

            _logs.Info("Apps.MyApps.Requested", context);

            var ownedTask = _apps.GetByOwnerUserIdAsync(userId);
            var memberAppIdsTask = _teamRepository.GetAllAppIdsByUserIdAsync(userId);

            await Task.WhenAll(ownedTask, memberAppIdsTask);

            var owned = ownedTask.Result ?? new List<MozaiksAppModel>();
            var memberIds = (memberAppIdsTask.Result ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            var member = memberIds.Count == 0
                ? new List<MozaiksAppModel>()
                : await _apps.GetByIdsAsync(memberIds);

            var response = new MyMozaiksAppsResponse
            {
                CorrelationId = correlationId,
                UserId = userId,
                OwnedApps = owned.Select(ToDto).ToList(),
                MemberApps = member.Select(ToDto).ToList()
            };

            _logs.Info("Apps.MyApps.Completed", context, new
            {
                ownedCount = response.OwnedApps.Count,
                memberCount = response.MemberApps.Count
            });

            return Ok(response);
        }

        [AllowAnonymous]
        [HttpGet("public")]
        public async Task<ActionResult<IEnumerable<MozaiksAppDto>>> GetPublicApps(CancellationToken cancellationToken)
        {
            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            var userId = GetCurrentUserId();
            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId
            };

            _logs.Info("Apps.Public.Requested", context);

            var apps = await _apps.GetPublicAsync();
            var result = (apps ?? new List<MozaiksAppModel>()).Select(ToDto).ToList();

            _logs.Info("Apps.Public.Completed", context, new { count = result.Count });

            return Ok(result);
        }

        [HttpGet("{appId}")]
        public async Task<ActionResult<MozaiksAppDto>> GetById(string appId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest();
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            var userId = GetCurrentUserId();
            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);
            Activity.Current?.SetTag("appId", appId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            _logs.Info("Apps.GetById.Requested", context);

            var app = await _apps.GetByIdAsync(appId);
            if (app == null)
            {
                _logs.Warn("Apps.GetById.NotFound", context);
                return NotFound(new { error = "NotFound" });
            }

            if (app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                _logs.Warn("Apps.GetById.Deleted", context);
                return NotFound(new { error = "NotFound" });
            }

            var dto = ToDto(app);

            _logs.Info("Apps.GetById.Completed", context);

            return Ok(dto);
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateMozaiksAppRequest request, IFormFile? image, CancellationToken cancellationToken)
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

            _logs.Info("Apps.Create.Requested", context);

            var app = new MozaiksAppModel
            {
                OwnerUserId = userId,
                Name = request.Name,
                Industry = request.Industry,
                Description = request.Description ?? string.Empty,
                IsPublicMozaik = string.Equals(request.Visibility, "PUBLIC", StringComparison.OrdinalIgnoreCase),
                IsActive = true
            };

            if (image != null && image.Length > 0)
            {
                var fileExtension = Path.GetExtension(image.FileName);
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                var imageDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                Directory.CreateDirectory(imageDir);
                var imagePath = Path.Combine(imageDir, uniqueFileName);

                await using var stream = new FileStream(imagePath, FileMode.Create);
                await image.CopyToAsync(stream, cancellationToken);

                app.LogoUrl = uniqueFileName;
            }

            await _apps.CreateAsync(app);

            Activity.Current?.SetTag("appId", app.Id);

            var dto = ToDto(app);

            _logs.Info("Apps.Create.Completed", context, new { appId = app.Id });

            return CreatedAtAction(nameof(GetById), new { appId = app.Id }, dto);
        }

        [HttpPatch("{appId}")]
        public async Task<IActionResult> Patch(string appId, [FromBody] UpdateMozaiksAppRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest();
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);
            Activity.Current?.SetTag("appId", appId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            _logs.Info("Apps.Patch.Requested", context);

            var app = await _apps.GetByIdAsync(appId);
            if (app == null)
            {
                _logs.Warn("Apps.Patch.NotFound", context);
                return NotFound(new { error = "NotFound" });
            }

            if (app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                _logs.Warn("Apps.Patch.Deleted", context);
                return Conflict(new { error = "invalid_state", message = "Cannot update a deleted app." });
            }

            if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (request.Name != null || request.Description != null || request.LogoUrl != null)
            {
                var patch = new AppConfigPatchRequest
                {
                    DisplayName = request.Name,
                    Description = request.Description,
                    AvatarUrl = request.LogoUrl
                };

                await _apps.PatchAppConfigAsync(appId, patch);
            }

            if (!string.IsNullOrWhiteSpace(request.Visibility))
            {
                var publish = string.Equals(request.Visibility, "PUBLIC", StringComparison.OrdinalIgnoreCase);
                await _apps.SetPublishStatusAsync(appId, publish);
            }

            _logs.Info("Apps.Patch.Completed", context);

            return NoContent();
        }

        [HttpPost("{appId}/pause")]
        public async Task<IActionResult> PauseApp(string appId, [FromBody] PauseResumeRequest? request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest();
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);
            Activity.Current?.SetTag("appId", appId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            _logs.Info("Apps.Pause.Requested", context);

            var result = await _lifecycle.PauseAsync(
                appId,
                userId,
                IsPlatformAdmin(),
                request?.Reason,
                correlationId,
                cancellationToken);

            if (result.Succeeded && result.Value is not null)
            {
                _logs.Info("Apps.Pause.Completed", context, new { status = result.Value.Status, result.Value.PausedAt });
                return Ok(result.Value);
            }

            _logs.Warn("Apps.Pause.Failed", context, new { error = result.Error, kind = result.FailureKind?.ToString() });
            return ToLifecycleActionResult(result);
        }

        [HttpPost("{appId}/resume")]
        public async Task<IActionResult> ResumeApp(string appId, [FromBody] PauseResumeRequest? request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest();
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);
            Activity.Current?.SetTag("appId", appId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            _logs.Info("Apps.Resume.Requested", context);

            var result = await _lifecycle.ResumeAsync(
                appId,
                userId,
                IsPlatformAdmin(),
                request?.Reason,
                correlationId,
                cancellationToken);

            if (result.Succeeded && result.Value is not null)
            {
                _logs.Info("Apps.Resume.Completed", context, new { status = result.Value.Status, result.Value.ResumedAt });
                return Ok(result.Value);
            }

            _logs.Warn("Apps.Resume.Failed", context, new { error = result.Error, kind = result.FailureKind?.ToString() });
            return ToLifecycleActionResult(result);
        }

        [HttpDelete("{appId}")]
        public async Task<IActionResult> DeleteApp(
            string appId,
            [FromBody] DeleteMozaiksAppRequest? request,
            [FromQuery] bool? confirm,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest();
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);
            Activity.Current?.SetTag("appId", appId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            _logs.Info("Apps.Delete.Requested", context);

            var confirmed = request?.Confirm == true || confirm == true;

            var result = await _lifecycle.SoftDeleteAsync(
                appId,
                userId,
                IsPlatformAdmin(),
                confirmed,
                request?.Reason,
                correlationId,
                cancellationToken);

            if (result.Succeeded && result.Value is not null)
            {
                _logs.Info("Apps.Delete.Completed", context, new { status = result.Value.Status, result.Value.DeletedAt, result.Value.HardDeleteAt });
                return Ok(result.Value);
            }

            _logs.Warn("Apps.Delete.Failed", context, new { error = result.Error, kind = result.FailureKind?.ToString() });
            return ToLifecycleActionResult(result);
        }

        [HttpPost("{appId}/delete")]
        public Task<IActionResult> DeleteAppViaPost(string appId, [FromBody] DeleteMozaiksAppRequest? request, CancellationToken cancellationToken)
            => DeleteApp(appId, request, confirm: null, cancellationToken);

        [HttpPatch("{appId}/feature-flags")]
        [Authorize(Policy = MozaiksAuthDefaults.RequireSuperAdminPolicy)]
        public async Task<IActionResult> ToggleFeatureFlag(string appId, [FromBody] FeatureFlagToggleRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest();
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("appId", appId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = GetCurrentUserId(),
                AppId = appId
            };

            _logs.Info("Apps.FeatureFlag.Requested", context, new { request.Flag, request.Enabled });

            var updated = await _apps.SetFeatureFlagAsync(appId, request.Flag, request.Enabled);
            if (!updated)
            {
                return NotFound(new { error = "NotFound" });
            }

            _logs.Info("Apps.FeatureFlag.Completed", context);

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

        private string GetOrCreateCorrelationId()
        {
            var header = Request.Headers["x-correlation-id"].ToString();
            return string.IsNullOrWhiteSpace(header) ? Guid.NewGuid().ToString() : header;
        }

        private IActionResult ToLifecycleActionResult(LifecycleOperationResult<DTOs.AppLifecycleStateResponse> result)
        {
            if (result.Succeeded && result.Value is not null)
            {
                return Ok(result.Value);
            }

            return result.FailureKind switch
            {
                LifecycleFailureKind.NotFound => NotFound(new { error = "NotFound" }),
                LifecycleFailureKind.Forbidden => Forbid(),
                LifecycleFailureKind.InvalidState => Conflict(new { error = "invalid_state", message = result.Error }),
                LifecycleFailureKind.BadRequest => BadRequest(new { error = "bad_request", message = result.Error }),
                _ => StatusCode(500, new { error = "LifecycleOperationFailed", message = result.Error ?? "Unknown error" })
            };
        }

        private MozaiksAppDto ToDto(MozaiksAppModel app)
        {
            var logo = app.LogoUrl;
            if (!string.IsNullOrWhiteSpace(logo) && !logo.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                logo = $"{Request.Scheme}://{Request.Host}/images/{logo}";
            }

            return new MozaiksAppDto
            {
                Id = app.Id ?? string.Empty,
                OwnerUserId = app.OwnerUserId,
                Name = app.Name,
                Description = app.Description,
                Visibility = app.IsPublicMozaik == true ? "PUBLIC" : "PRIVATE",
                Investable = false,
                Monetized = false,
                LogoUrl = logo,
                Industry = app.Industry,
                IsActive = app.IsActive,
                CreatedAt = app.CreatedAt,
                UpdatedAt = app.UpdatedAt
            };
        }
    }
}

