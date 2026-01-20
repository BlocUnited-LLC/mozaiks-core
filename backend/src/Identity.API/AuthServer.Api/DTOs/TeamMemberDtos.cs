using System.ComponentModel.DataAnnotations;

namespace AuthServer.Api.DTOs
{
    public static class TeamMemberRoles
    {
        public const string Owner = "Owner";
        public const string Admin = "Admin";
        public const string Member = "Member";
    }

    public class TeamMemberDto
    {
        public string MemberId { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public string Role { get; set; } = TeamMemberRoles.Member;

        /// <summary>
        /// Basis points (0..10000). 10000 = 100%.
        /// Metadata only; MozaiksPay is source of truth for wallets/ledger.
        /// </summary>
        public int MpAllocationBps { get; set; } = 0;

        public string? Note { get; set; }
        public DateTime JoinedAtUtc { get; set; }
    }

    public class TeamMembersResponse
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public List<TeamMemberDto> Members { get; set; } = new();
    }

    public class UpdateTeamMemberRequest
    {
        [Required]
        public string Role { get; set; } = TeamMemberRoles.Member;

        [Range(0, 10000)]
        public int MpAllocationBps { get; set; } = 0;

        public string? Note { get; set; }
    }

    public class CreateTeamInviteRequest
    {
        /// <summary>
        /// Recipient user id (preferred) OR email.
        /// </summary>
        public string? RecipientUserId { get; set; }

        public string? RecipientEmail { get; set; }

        public string Role { get; set; } = TeamMemberRoles.Member;

        [Range(0, 10000)]
        public int MpAllocationBps { get; set; } = 0;

        public string? Note { get; set; }
    }
}

