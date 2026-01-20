using System.Text.Json.Serialization;

namespace AuthServer.Api.DTOs
{
    public class MeDashboardResponse
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? Username { get; set; }

        // Creator dashboard (Phase A) - included for UI consolidation.
        public CreatorDashboardSummary Summary { get; set; } = new();
        public List<CreatorDashboardAppItem> Apps { get; set; } = new();
        public List<CreatorDashboardActivityItem> RecentActivity { get; set; } = new();
        public List<CreatorDashboardAlertItem> Alerts { get; set; } = new();

        public IEnumerable<MozaikSummaryDto> OwnedMozaiks { get; set; } = Array.Empty<MozaikSummaryDto>();
        public IEnumerable<MozaikSummaryDto> MemberMozaiks { get; set; } = Array.Empty<MozaikSummaryDto>();

        public IEnumerable<InvestorPositionDashboardDto> InvestorPositions { get; set; } = Array.Empty<InvestorPositionDashboardDto>();
        public IEnumerable<InvestmentListItemDto> Investments { get; set; } = Array.Empty<InvestmentListItemDto>();

        public IEnumerable<DashboardErrorDto> Errors { get; set; } = Array.Empty<DashboardErrorDto>();
    }

    public class MozaikSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? Industry { get; set; }
        public string Visibility { get; set; } = "PRIVATE";
        public string OwnerUserId { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class InvestorPositionDashboardDto
    {
        public string AppId { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public int TotalMp { get; set; }

        // Dashboard contract compatibility: provide fields used by UI aggregators.
        // When Governance doesn't return these yet, AuthServer will derive mpHeld from totalMp.
        [JsonPropertyName("mpHeld")]
        public int MpHeld { get; set; }

        [JsonPropertyName("currentValue")]
        public decimal? CurrentValue { get; set; }

        [JsonPropertyName("acquisitionHistory")]
        public IEnumerable<object> AcquisitionHistory { get; set; } = Array.Empty<object>();
    }

    public class InvestmentListItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public string RoundId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public long MpGranted { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? WalletId { get; set; }
        public string? PaymentIntentId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DashboardErrorDto
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
