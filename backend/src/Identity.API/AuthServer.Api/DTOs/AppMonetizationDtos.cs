using System.Text.Json.Serialization;
using AuthServer.Api.Models;

namespace AuthServer.Api.DTOs;

public sealed class AppMonetizationCommitRequest
{
    [JsonPropertyName("spec")]
    public AppMonetizationSpec Spec { get; set; } = new();

    [JsonPropertyName("specHash")]
    public string SpecHash { get; set; } = string.Empty;

    [JsonPropertyName("approvedByUserId")]
    public string ApprovedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("approvedAtUtc")]
    public DateTime? ApprovedAtUtc { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("proposalId")]
    public string? ProposalId { get; set; }
}

public sealed class AppMonetizationCommitResponse
{
    public string AppId { get; set; } = string.Empty;
    public string SpecHash { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? StripeStatus { get; set; }
    public List<AppMonetizationPlanProvisioningResponse> Plans { get; set; } = new();
}

public sealed class AppMonetizationPlanProvisioningResponse
{
    public string PlanId { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public string? StripeProductId { get; set; }
    public string? StripePriceId { get; set; }
    public string? StripeLookupKey { get; set; }
}
