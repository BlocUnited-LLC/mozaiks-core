using MongoDB.Bson.Serialization.Attributes;

namespace Payment.API.Models;

/// <summary>
/// Append-only Economic Protocol event store (v1).
/// Raw JSON is persisted for replay + future schema evolution; key fields are extracted for indexing/query.
/// </summary>
public sealed class EconomicEventDocument : DocumentBase
{
    [BsonElement("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("eventVersion")]
    public int EventVersion { get; set; } = 1;

    [BsonElement("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; }

    [BsonElement("appId")]
    public string AppId { get; set; } = string.Empty;

    [BsonElement("env")]
    public string Env { get; set; } = string.Empty;

    // Source
    [BsonElement("producer")]
    public string Producer { get; set; } = string.Empty;

    [BsonElement("service")]
    public string Service { get; set; } = string.Empty;

    [BsonElement("requestId")]
    public string? RequestId { get; set; }

    [BsonElement("ip")]
    public string? Ip { get; set; }

    // Actor
    [BsonElement("actorType")]
    public string ActorType { get; set; } = "system";

    [BsonElement("actorId")]
    public string? ActorId { get; set; }

    // Correlation (flattened for query)
    [BsonElement("campaignId")]
    public string? CampaignId { get; set; }

    [BsonElement("roundId")]
    public string? RoundId { get; set; }

    [BsonElement("commitmentId")]
    public string? CommitmentId { get; set; }

    [BsonElement("allocationId")]
    public string? AllocationId { get; set; }

    [BsonElement("transactionId")]
    public string? TransactionId { get; set; }

    [BsonElement("userId")]
    public string? UserId { get; set; }

    /// <summary>
    /// Raw envelope JSON for replay/training. Keep PII out by default.
    /// </summary>
    [BsonElement("envelopeJson")]
    public string EnvelopeJson { get; set; } = string.Empty;
}

