using System.ComponentModel.DataAnnotations;

namespace AuthServer.Api.DTOs
{
    public class AppConfigPatchRequest
    {
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
        public List<string>? InstalledPlugins { get; set; }
    }

    public class PublishStatusRequest
    {
        public bool Publish { get; set; }
    }

    public class FeatureFlagToggleRequest
    {
        [Required]
        public string Flag { get; set; } = string.Empty;

        public bool Enabled { get; set; }
    }
}

