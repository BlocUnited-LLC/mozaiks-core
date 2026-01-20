using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventBus.Messages.EconomicProtocol;

/// <summary>
/// Economic Protocol v1 envelope. Payload is event-type specific.
/// This is designed for cross-language producers (runtime/plugins) and deterministic ingestion in the control plane.
/// </summary>
public sealed class EconomicEventEnvelope
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "1.0";

    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("event_version")]
    public int EventVersion { get; set; } = 1;

    [JsonPropertyName("occurred_at")]
    public DateTimeOffset OccurredAt { get; set; }

    [JsonPropertyName("source")]
    public EconomicEventSource Source { get; set; } = new();

    [JsonPropertyName("actor")]
    public EconomicEventActor Actor { get; set; } = new();

    [JsonPropertyName("correlation")]
    public EconomicEventCorrelation Correlation { get; set; } = new();

    /// <summary>
    /// Event-type specific payload. Keep PII out of this by default.
    /// </summary>
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }

    /// <summary>
    /// Optional signature for runtime-produced events (HMAC/mTLS in later phases).
    /// </summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
}

public sealed class EconomicEventSource
{
    /// <summary>
    /// "control_plane" or "runtime"
    /// </summary>
    [JsonPropertyName("producer")]
    public string Producer { get; set; } = string.Empty;

    /// <summary>
    /// "GrowthEngine", "governance", "payment", "insights", "runtime_shell", ...
    /// </summary>
    [JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    [JsonPropertyName("app_id")]
    public string AppId { get; set; } = string.Empty;

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("ip")]
    public string? Ip { get; set; }
}

public sealed class EconomicEventActor
{
    /// <summary>
    /// "user", "system", or "app"
    /// </summary>
    [JsonPropertyName("actor_type")]
    public string ActorType { get; set; } = "system";

    [JsonPropertyName("actor_id")]
    public string? ActorId { get; set; }
}

public sealed class EconomicEventCorrelation
{
    [JsonPropertyName("campaign_id")]
    public string? CampaignId { get; set; }

    [JsonPropertyName("round_id")]
    public string? RoundId { get; set; }

    [JsonPropertyName("commitment_id")]
    public string? CommitmentId { get; set; }

    [JsonPropertyName("allocation_id")]
    public string? AllocationId { get; set; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }
}

