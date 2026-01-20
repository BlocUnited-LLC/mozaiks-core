namespace AuthServer.Api.DTOs
{
    public class NotificationSettingsDto
    {
        public bool Deployments { get; set; } = true;
        public bool Errors { get; set; } = true;
        public bool FundingUpdates { get; set; } = true;
    }

    public class MeSettingsResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Timezone { get; set; } = "UTC";
        public NotificationSettingsDto Notifications { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    public class MeSettingsUpdateRequest
    {
        public string? Timezone { get; set; }
        public NotificationSettingsDto? Notifications { get; set; }
    }

    public class PlatformFeaturesDto
    {
        public bool Funding { get; set; } = true;
        public bool E2bValidation { get; set; } = true;
    }

    public class PaginationSettingsDto
    {
        public int DefaultPageSize { get; set; } = 20;
        public int MaxPageSize { get; set; } = 100;
    }

    public class PlatformSettingsResponse
    {
        public PlatformFeaturesDto Features { get; set; } = new();
        public PaginationSettingsDto Pagination { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedByUserId { get; set; }
    }

    public class PlatformSettingsUpdateRequest
    {
        public PlatformFeaturesDto? Features { get; set; }
        public PaginationSettingsDto? Pagination { get; set; }
    }
}
