namespace AuthServer.Api.DTOs;

public sealed class EntitlementsResponse
{
    public bool AllowHosting { get; set; }
    public bool AllowExportRepo { get; set; }
    public bool AllowWorkerMode { get; set; }
    public string? HostingLevel { get; set; }
    public string? AppPlanId { get; set; }
    public string? PlatformPlanId { get; set; }
    public EntitlementDomains Domains { get; set; } = new();
    public EntitlementEmail Email { get; set; } = new();
    public EntitlementTokens Tokens { get; set; } = new();
    public EntitlementHosting Hosting { get; set; } = new();
    public EntitlementFees Fees { get; set; } = new();
    public EntitlementBilling Billing { get; set; } = new();
}

public sealed class EntitlementDomains
{
    public int? Max { get; set; }
    public int? Used { get; set; }
}

public sealed class EntitlementEmail
{
    public bool Enabled { get; set; }
    public int? MaxDomains { get; set; }
}

public sealed class EntitlementTokens
{
    public int? MonthlyLimit { get; set; }
}

public sealed class EntitlementHosting
{
    public string? Tier { get; set; }
}

public sealed class EntitlementFees
{
    public int? TransactionFeeBps { get; set; }
}

public sealed class EntitlementBilling
{
    public string? StripeSubscriptionId { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
}
