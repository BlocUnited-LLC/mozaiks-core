namespace AuthServer.Api.DTOs
{
    public class MozaiksAppDto
    {
        public string Id { get; set; } = string.Empty;
        public string OwnerUserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Visibility { get; set; } = "PRIVATE";
        public bool Investable { get; set; } = false;
        public bool Monetized { get; set; } = false;
        public string? LogoUrl { get; set; }
        public string? Industry { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class MyMozaiksAppsResponse
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public List<MozaiksAppDto> OwnedApps { get; set; } = new();
        public List<MozaiksAppDto> MemberApps { get; set; } = new();
    }

    public class CreateMozaiksAppRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Industry { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// "PUBLIC" or "PRIVATE" (defaults to PRIVATE).
        /// </summary>
        public string? Visibility { get; set; }
    }

    public class UpdateMozaiksAppRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }

        /// <summary>
        /// "PUBLIC" or "PRIVATE" (optional).
        /// </summary>
        public string? Visibility { get; set; }
        
        public List<string>? InstalledPlugins { get; set; }
    }
}

