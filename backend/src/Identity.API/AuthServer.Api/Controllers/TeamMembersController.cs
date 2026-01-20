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
    [Route("api/apps/{appId}/team-members")]
    [ApiController]
    [Authorize]
    public class TeamMembersController : ControllerBase
    {
        private readonly MozaiksAppService _apps;
        private readonly ITeamMembersRepository _teamMembers;
        private readonly IUserRepository _users;
        private readonly IInviteRepository _invites;
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public TeamMembersController(
            MozaiksAppService apps,
            ITeamMembersRepository teamMembers,
            IUserRepository users,
            IInviteRepository invites,
            StructuredLogEmitter logs,
            IUserContextAccessor userContextAccessor)
        {
            _apps = apps;
            _teamMembers = teamMembers;
            _users = users;
            _invites = invites;
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpGet]
        public async Task<ActionResult<TeamMembersResponse>> GetTeamMembers(string appId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
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

            _logs.Info("Apps.TeamMembers.List.Requested", context);

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                _logs.Warn("Apps.TeamMembers.List.AppNotFound", context);
                return NotFound(new { error = "NotFound", reason = "AppNotFound" });
            }

            var isOwner = string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase);
            var isPlatformAdmin = IsPlatformAdmin();
            var isMember = isOwner;
            if (!isMember && !isPlatformAdmin)
            {
                var member = await _teamMembers.GetByAppAndUserIdAsync(appId, userId);
                isMember = member is not null;
            }

            if (!isMember && !isPlatformAdmin)
            {
                _logs.Warn("Apps.TeamMembers.List.Forbidden", context);
                return Forbid();
            }

            var members = (await _teamMembers.GetAllAsync(appId)).ToList();
            var userIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                app.OwnerUserId
            };
            foreach (var m in members)
            {
                if (!string.IsNullOrWhiteSpace(m.UserId))
                {
                    userIds.Add(m.UserId);
                }
            }

            var users = await _users.GetUsersByIdsAsync(userIds);
            var userById = users
                .Where(u => !string.IsNullOrWhiteSpace(u.Id))
                .ToDictionary(u => u.Id!, StringComparer.OrdinalIgnoreCase);

            var result = new List<TeamMemberDto>();

            // Owner first
            if (userById.TryGetValue(app.OwnerUserId, out var ownerUser))
            {
                result.Add(ToTeamMemberDto(
                    memberId: string.Empty,
                    appId: appId,
                    user: ownerUser,
                    role: TeamMemberRoles.Owner,
                    mpAllocationBps: 0,
                    note: null,
                    joinedAtUtc: app.CreatedAt));
            }

            foreach (var m in members)
            {
                if (string.Equals(m.UserId, app.OwnerUserId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!userById.TryGetValue(m.UserId, out var memberUser))
                {
                    continue;
                }

                result.Add(ToTeamMemberDto(
                    memberId: m.Id ?? string.Empty,
                    appId: appId,
                    user: memberUser,
                    role: NormalizeTeamRole(m.Role),
                    mpAllocationBps: m.MpAllocationBps,
                    note: m.Note,
                    joinedAtUtc: m.CreatedAt));
            }

            var response = new TeamMembersResponse
            {
                CorrelationId = correlationId,
                AppId = appId,
                Members = result
            };

            _logs.Info("Apps.TeamMembers.List.Completed", context, new { count = response.Members.Count });

            return Ok(response);
        }

        [HttpPatch("{memberUserId}")]
        public async Task<IActionResult> PatchTeamMember(
            string appId,
            string memberUserId,
            [FromBody] UpdateTeamMemberRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(memberUserId))
            {
                return BadRequest(new { error = "InvalidRequest" });
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
            Activity.Current?.SetTag("memberUserId", memberUserId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            _logs.Info("Apps.TeamMembers.Patch.Requested", context, new
            {
                memberUserId,
                request.Role,
                request.MpAllocationBps
            });

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                return NotFound(new { error = "NotFound", reason = "AppNotFound" });
            }

            var isOwner = string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase);
            if (!isOwner && !IsPlatformAdmin())
            {
                return Forbid();
            }

            if (string.Equals(memberUserId, app.OwnerUserId, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "InvalidOperation", reason = "CannotModifyOwner" });
            }

            var existing = await _teamMembers.GetByAppAndUserIdAsync(appId, memberUserId);
            if (existing is null)
            {
                return NotFound(new { error = "NotFound", reason = "TeamMemberNotFound" });
            }

            var role = NormalizeTeamRole(request.Role);
            if (role == TeamMemberRoles.Owner)
            {
                return BadRequest(new { error = "InvalidRole", reason = "OwnerRoleNotAssignable" });
            }

            await _teamMembers.UpdateByAppAndUserIdAsync(appId, memberUserId, role, request.MpAllocationBps, request.Note);

            _logs.Info("Apps.TeamMembers.Patch.Completed", context);

            return NoContent();
        }

        [HttpPost("invites")]
        public async Task<IActionResult> CreateInvite(
            string appId,
            [FromBody] CreateTeamInviteRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
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

            _logs.Info("Apps.TeamMembers.Invite.Create.Requested", context);

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                return NotFound(new { error = "NotFound", reason = "AppNotFound" });
            }

            var isOwner = string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase);
            if (!isOwner && !IsPlatformAdmin())
            {
                return Forbid();
            }

            var recipientUser = await ResolveRecipientUserAsync(request);
            if (recipientUser is null || string.IsNullOrWhiteSpace(recipientUser.Id))
            {
                return NotFound(new { error = "NotFound", reason = "RecipientNotFound" });
            }

            if (string.Equals(recipientUser.Id, userId, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "InvalidRecipient", reason = "CannotInviteSelf" });
            }

            if (string.Equals(recipientUser.Id, app.OwnerUserId, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "InvalidRecipient", reason = "OwnerAlreadyMember" });
            }

            var proposedRole = NormalizeTeamRole(request.Role);
            if (proposedRole == TeamMemberRoles.Owner)
            {
                return BadRequest(new { error = "InvalidRole", reason = "OwnerRoleNotInvitable" });
            }

            var alreadyMember = await _teamMembers.GetByAppAndUserIdAsync(appId, recipientUser.Id);
            if (alreadyMember is not null)
            {
                return Conflict(new { error = "AlreadyMember" });
            }

            var dupCount = _invites.CheckDuplicateInvites(userId, recipientUser.Id, appId);
            if (dupCount > 0)
            {
                return Conflict(new { error = "DuplicateInvite" });
            }

            var sender = _userContextAccessor.GetUser(User);
            var senderEmail = sender?.Email ?? string.Empty;
            var senderName = sender?.DisplayName ?? string.Empty;

            var invite = new InviteModel
            {
                AppId = appId,
                AppName = app.Name,
                InvitedByUserId = userId,
                ReceipentUserId = recipientUser.Id,
                SenderEmail = senderEmail,
                ReceiverEmail = recipientUser.Email,
                SenderUserName = senderName,
                InvitationMessage = request.Note ?? string.Empty,
                InviteStatus = 1,
                ProposedRole = proposedRole,
                ProposedMpAllocationBps = request.MpAllocationBps,
                ProposedNote = request.Note,
            };

            await _invites.CreateInviteAsync(invite);

            _logs.Info("Apps.TeamMembers.Invite.Create.Completed", context, new { inviteId = invite.Id, recipientUserId = recipientUser.Id });

            return CreatedAtAction(nameof(GetTeamMembers), new { appId }, new { inviteId = invite.Id });
        }

        [HttpPost("invites/{inviteId}/accept")]
        public async Task<IActionResult> AcceptInvite(string appId, string inviteId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(inviteId))
            {
                return BadRequest(new { error = "InvalidRequest" });
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

            _logs.Info("Apps.TeamMembers.Invite.Accept.Requested", context, new { inviteId });

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                return NotFound(new { error = "NotFound", reason = "AppNotFound" });
            }

            var invite = await _invites.GetInviteByIdAsync(inviteId);
            if (invite is null || !string.Equals(invite.AppId, appId, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = "NotFound", reason = "InviteNotFound" });
            }

            if (!string.Equals(invite.ReceipentUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (invite.InviteStatus != 1)
            {
                return Conflict(new { error = "InviteNotPending", status = invite.InviteStatus });
            }

            // Mark accepted
            await _invites.UpdateInviteStatusAsync(inviteId, 2);

            var member = new TeamMembersModel
            {
                AppId = appId,
                UserId = userId,
                InvitedByUserId = invite.InvitedByUserId,
                Role = NormalizeTeamRole(invite.ProposedRole ?? TeamMemberRoles.Member),
                MpAllocationBps = invite.ProposedMpAllocationBps ?? 0,
                Note = invite.ProposedNote,
                MemberStatus = 1
            };

            await _teamMembers.UpsertAsync(member);

            _logs.Info("Apps.TeamMembers.Invite.Accept.Completed", context, new { inviteId });

            return Ok(new { success = true });
        }

        [HttpPost("invites/{inviteId}/reject")]
        public async Task<IActionResult> RejectInvite(string appId, string inviteId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(inviteId))
            {
                return BadRequest(new { error = "InvalidRequest" });
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                return NotFound(new { error = "NotFound", reason = "AppNotFound" });
            }

            var invite = await _invites.GetInviteByIdAsync(inviteId);
            if (invite is null || !string.Equals(invite.AppId, appId, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = "NotFound", reason = "InviteNotFound" });
            }

            if (!string.Equals(invite.ReceipentUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (invite.InviteStatus != 1)
            {
                return Conflict(new { error = "InviteNotPending", status = invite.InviteStatus });
            }

            await _invites.UpdateInviteStatusAsync(inviteId, 3);
            _logs.Info("Apps.TeamMembers.Invite.Reject.Completed", context, new { inviteId });

            return Ok(new { success = true });
        }

        [HttpGet("invites")]
        public async Task<IActionResult> GetPendingInvitesForApp(string appId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                return NotFound(new { error = "NotFound", reason = "AppNotFound" });
            }

            var isOwner = string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase);
            if (!isOwner && !IsPlatformAdmin())
            {
                return Forbid();
            }

            // MVP: reuse existing repository method and filter client-side.
            // We can add an indexed query by appId later if this grows.
            var sent = await _invites.GetSentInvitesAsync(userId);
            var pending = sent.Where(i => i.InviteStatus == 1 && string.Equals(i.AppId, appId, StringComparison.OrdinalIgnoreCase))
                .Select(i => new
                {
                    inviteId = i.Id,
                    appId = i.AppId,
                    appName = i.AppName,
                    recipientUserId = i.ReceipentUserId,
                    receiverEmail = i.ReceiverEmail,
                    status = i.InviteStatus,
                    role = i.ProposedRole,
                    mpAllocationBps = i.ProposedMpAllocationBps,
                    note = i.ProposedNote,
                    createdAtUtc = i.CreatedAt
                })
                .ToList();

            return Ok(new
            {
                correlationId,
                appId,
                invites = pending
            });
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

        private static string NormalizeTeamRole(string? role)
        {
            var normalized = (role ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return TeamMemberRoles.Member;
            }

            if (string.Equals(normalized, TeamMemberRoles.Owner, StringComparison.OrdinalIgnoreCase))
            {
                return TeamMemberRoles.Owner;
            }

            if (string.Equals(normalized, TeamMemberRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return TeamMemberRoles.Admin;
            }

            return TeamMemberRoles.Member;
        }

        private TeamMemberDto ToTeamMemberDto(
            string memberId,
            string appId,
            UserModel user,
            string role,
            int mpAllocationBps,
            string? note,
            DateTime joinedAtUtc)
        {
            var displayName = $"{user.FirstName} {user.LastName}".Trim();
            var photoUrl = user.UserPhoto;
            if (!string.IsNullOrWhiteSpace(photoUrl) && !photoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                photoUrl = $"{Request.Scheme}://{Request.Host}/images/{photoUrl}";
            }

            return new TeamMemberDto
            {
                MemberId = memberId,
                AppId = appId,
                UserId = user.Id ?? string.Empty,
                DisplayName = displayName,
                Email = user.Email,
                PhotoUrl = photoUrl,
                Role = role,
                MpAllocationBps = mpAllocationBps,
                Note = note,
                JoinedAtUtc = joinedAtUtc
            };
        }

        private async Task<UserModel?> ResolveRecipientUserAsync(CreateTeamInviteRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.RecipientUserId))
            {
                return await _users.GetUserByIdAsync(request.RecipientUserId);
            }

            if (!string.IsNullOrWhiteSpace(request.RecipientEmail))
            {
                return await _users.GetUserByEmailAsync(request.RecipientEmail);
            }

            return null;
        }
    }
}
