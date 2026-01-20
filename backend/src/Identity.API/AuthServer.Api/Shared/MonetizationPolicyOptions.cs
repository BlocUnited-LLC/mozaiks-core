namespace AuthServer.Api.Shared;

public sealed class MonetizationPolicyOptions
{
    public long PlanPriceFloorCents { get; set; } = 0;
    public long PlanPriceCeilingCents { get; set; } = 1_000_000;
    public long AddOnPriceFloorCents { get; set; } = 0;
    public long AddOnPriceCeilingCents { get; set; } = 1_000_000;
    public int TransactionFeeBpsFloor { get; set; } = 0;
    public int TransactionFeeBpsCeiling { get; set; } = 3_000;
    public int MaxPriceDeltaBps { get; set; } = 5_000;
    public int MaxTransactionFeeDeltaBps { get; set; } = 500;
    public int CooldownMinutes { get; set; } = 60;
    public int TokensMonthlyFloor { get; set; } = 0;
    public int TokensMonthlyCeiling { get; set; } = 1_000_000;
    public int DomainsMaxFloor { get; set; } = 0;
    public int DomainsMaxCeiling { get; set; } = 1_000;
    public int EmailMaxDomainsFloor { get; set; } = 0;
    public int EmailMaxDomainsCeiling { get; set; } = 1_000;
}
