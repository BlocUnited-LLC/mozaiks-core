namespace AuthServer.Api.DTOs
{
    public sealed class ConfigureAdminSurfaceRequest
    {
        public string? BaseUrl { get; set; }
        public string? AdminKey { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class ConfigureAdminSurfaceResponse
    {
        public bool Ok { get; set; } = true;
        public string AppId { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public bool Configured { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AdminSurfaceStatusResponse
    {
        public string AppId { get; set; } = string.Empty;
        public bool Configured { get; set; }
        public string? BaseUrl { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

