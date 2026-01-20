namespace AuthServer.Api.DTOs;

public sealed class CreatorDashboardResponse
{
    public string UserId { get; set; } = string.Empty;
    public string? Username { get; set; }

    public CreatorDashboardSummary Summary { get; set; } = new();

    public List<CreatorDashboardAppItem> Apps { get; set; } = new();

    public List<CreatorDashboardActivityItem> RecentActivity { get; set; } = new();

    public List<CreatorDashboardAlertItem> Alerts { get; set; } = new();
}

public sealed class CreatorDashboardSummary
{
    public int TotalApps { get; set; }
    public Dictionary<string, int> AppsByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int TotalEvents24h { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers24h { get; set; }
    public int TotalErrors24h { get; set; }
    public double ErrorRate { get; set; }
}

public sealed class CreatorDashboardAppItem
{
    public string AppId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastDeployedAt { get; set; }

    public CreatorDashboardAppStats Stats { get; set; } = new();
    public CreatorDashboardDatabaseStatus Database { get; set; } = new();
    public bool SdkConnected { get; set; }
}

public sealed class CreatorDashboardAppStats
{
    public int Events24h { get; set; }
    public int Users { get; set; }
    public int ActiveUsers24h { get; set; }
    public int Errors24h { get; set; }
    public double ErrorRate { get; set; }
}

public sealed class CreatorDashboardDatabaseStatus
{
    public bool Provisioned { get; set; }
    public string? Name { get; set; }
}

public sealed class CreatorDashboardActivityItem
{
    public string Type { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CreatorDashboardAlertItem
{
    public string Severity { get; set; } = "info";
    public string AppId { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

