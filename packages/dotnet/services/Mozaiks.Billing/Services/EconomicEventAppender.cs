using System.Text.Json;
using EventBus.Messages.EconomicProtocol;
using Payment.API.Models;
using Payment.API.Repository.Interfaces;

namespace Payment.API.Services;

public sealed class EconomicEventAppender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IEconomicEventRepository _repo;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EconomicEventAppender> _logger;

    public EconomicEventAppender(
        IEconomicEventRepository repo,
        IWebHostEnvironment env,
        ILogger<EconomicEventAppender> logger)
    {
        _repo = repo;
        _env = env;
        _logger = logger;
    }

    public async Task TryAppendAsync(EconomicEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(envelope.EventId) || string.IsNullOrWhiteSpace(envelope.EventType))
        {
            _logger.LogWarning("Economic event missing required fields (eventId/eventType).");
            return;
        }

        var appId = envelope.Source.AppId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(appId))
        {
            _logger.LogWarning("Economic event missing required source.app_id.");
            return;
        }

        var resolvedEnv = string.IsNullOrWhiteSpace(envelope.Source.Environment)
            ? GetDefaultEnv()
            : envelope.Source.Environment.Trim();

        var occurredAt = envelope.OccurredAt == default ? DateTimeOffset.UtcNow : envelope.OccurredAt;

        var now = DateTime.UtcNow;
        var envelopeJson = JsonSerializer.Serialize(envelope, JsonOptions);

        var doc = new EconomicEventDocument
        {
            SchemaVersion = string.IsNullOrWhiteSpace(envelope.SchemaVersion) ? "1.0" : envelope.SchemaVersion.Trim(),
            EventId = envelope.EventId.Trim(),
            EventType = envelope.EventType.Trim(),
            EventVersion = envelope.EventVersion <= 0 ? 1 : envelope.EventVersion,
            OccurredAtUtc = occurredAt.UtcDateTime,
            AppId = appId,
            Env = resolvedEnv,
            Producer = envelope.Source.Producer?.Trim() ?? string.Empty,
            Service = envelope.Source.Service?.Trim() ?? string.Empty,
            RequestId = envelope.Source.RequestId?.Trim(),
            Ip = envelope.Source.Ip?.Trim(),
            ActorType = string.IsNullOrWhiteSpace(envelope.Actor.ActorType) ? "system" : envelope.Actor.ActorType.Trim(),
            ActorId = envelope.Actor.ActorId?.Trim(),
            CampaignId = envelope.Correlation.CampaignId?.Trim(),
            RoundId = envelope.Correlation.RoundId?.Trim(),
            CommitmentId = envelope.Correlation.CommitmentId?.Trim(),
            AllocationId = envelope.Correlation.AllocationId?.Trim(),
            TransactionId = envelope.Correlation.TransactionId?.Trim(),
            UserId = envelope.Correlation.UserId?.Trim(),
            EnvelopeJson = envelopeJson,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repo.InsertManyAsync(new[] { doc }, cancellationToken);
    }

    private string GetDefaultEnv()
        => _env.IsDevelopment() ? "development" : "production";
}
