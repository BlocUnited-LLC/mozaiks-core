using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppMonetizationSpecStatus
{
    Draft,
    Committed,
    Archived
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppMonetizationPlanAvailability
{
    AllSubscribers,
    NewSubscribersOnly
}

public sealed class AppMonetizationSpec
{
    public string Currency { get; set; } = "usd";
    public List<AppMonetizationPlanSpec> Plans { get; set; } = new();
    public List<AppMonetizationAddOnSpec> AddOns { get; set; } = new();
}

public sealed class AppMonetizationPlanSpec
{
    public string PlanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long PriceCents { get; set; }
    public string Currency { get; set; } = "usd";
    public string BillingInterval { get; set; } = "month";
    [BsonRepresentation(BsonType.String)]
    public AppMonetizationPlanAvailability Availability { get; set; } = AppMonetizationPlanAvailability.NewSubscribersOnly;
    public AppMonetizationEntitlementsSpec Entitlements { get; set; } = new();
}

public sealed class AppMonetizationAddOnSpec
{
    public string AddOnId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long PriceCents { get; set; }
    public string Currency { get; set; } = "usd";
    public string BillingInterval { get; set; } = "month";
    public AppMonetizationEntitlementsDelta EntitlementsDelta { get; set; } = new();
}

public sealed class AppMonetizationEntitlementsSpec
{
    public int? MonthlyTokens { get; set; }
    public string? HostingTier { get; set; }
    public int? DomainsMax { get; set; }
    public bool EmailEnabled { get; set; }
    public int? EmailMaxDomains { get; set; }
    public int? TransactionFeeBps { get; set; }
}

public sealed class AppMonetizationEntitlementsDelta
{
    public int? MonthlyTokensDelta { get; set; }
    public int? DomainsMaxDelta { get; set; }
    public int? EmailMaxDomainsDelta { get; set; }
    public bool? EmailEnabled { get; set; }
    public string? HostingTier { get; set; }
    public int? TransactionFeeBps { get; set; }
}

public sealed class AppMonetizationPlanVersion
{
    public string PlanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long PriceCents { get; set; }
    public string Currency { get; set; } = "usd";
    public string BillingInterval { get; set; } = "month";
    [BsonRepresentation(BsonType.String)]
    public AppMonetizationPlanAvailability Availability { get; set; } = AppMonetizationPlanAvailability.NewSubscribersOnly;
    public AppMonetizationEntitlementsSpec Entitlements { get; set; } = new();
    public StripePlanMapping Stripe { get; set; } = new();
}

public sealed class StripePlanMapping
{
    public string? ProductId { get; set; }
    public string? PriceId { get; set; }
    public string? LookupKey { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
}

public sealed class AppMonetizationSpecVersion : DocumentBase
{
    [BsonElement("appId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AppId { get; set; } = string.Empty;

    [BsonElement("specHash")]
    public string SpecHash { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [BsonElement("createdByUserId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public AppMonetizationSpecStatus Status { get; set; } = AppMonetizationSpecStatus.Draft;

    [BsonElement("approvedByUserId")]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? ApprovedByUserId { get; set; }

    [BsonElement("approvedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? ApprovedAtUtc { get; set; }

    [BsonElement("source")]
    [BsonIgnoreIfNull]
    public string? Source { get; set; }

    [BsonElement("proposalId")]
    [BsonIgnoreIfNull]
    public string? ProposalId { get; set; }

    [BsonElement("spec")]
    public AppMonetizationSpec Spec { get; set; } = new();

    [BsonElement("planVersions")]
    public List<AppMonetizationPlanVersion> PlanVersions { get; set; } = new();
}
