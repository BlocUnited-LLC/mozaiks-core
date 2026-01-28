namespace Payment.API.DTOs;

public sealed class MonetizationPriceProvisionRequest
{
    public string AppId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "usd";
    public string BillingInterval { get; set; } = "month";
    public int SpecVersion { get; set; }
    public string SpecHash { get; set; } = string.Empty;
    public string? ProposalId { get; set; }
}

public sealed class MonetizationPriceProvisionResponse
{
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public string? StripeProductId { get; set; }
    public string? StripePriceId { get; set; }
    public string? StripeLookupKey { get; set; }
}
