using System.Text.Json;
using EventBus.Messages.EconomicProtocol;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;
using Payment.API.Models;
using Payment.API.Repository.Interfaces;

namespace Payment.API.Controllers;

[ApiController]
[Route("api/internal/economic/events")]
[Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
public sealed class EconomicEventsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IEconomicEventRepository _repo;
    private readonly IWebHostEnvironment _env;

    public EconomicEventsController(
        IEconomicEventRepository repo,
        IWebHostEnvironment env)
    {
        _repo = repo;
        _env = env;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IngestEconomicEventsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AppId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        if (request.Events == null || request.Events.Count == 0)
        {
            return BadRequest(new { error = "NoEvents" });
        }

        var resolvedEnv = string.IsNullOrWhiteSpace(request.Env) ? GetDefaultEnv() : request.Env.Trim();
        var appId = request.AppId.Trim();
        var now = DateTime.UtcNow;

        var docs = new List<EconomicEventDocument>(request.Events.Count);

        foreach (var e in request.Events)
        {
            if (string.IsNullOrWhiteSpace(e.EventId) || string.IsNullOrWhiteSpace(e.EventType))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(e.Source.AppId)
                && !string.Equals(e.Source.AppId.Trim(), appId, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "AppIdMismatch", envelopeAppId = e.Source.AppId, requestAppId = appId });
            }

            var envelopeEnv = string.IsNullOrWhiteSpace(e.Source.Environment) ? resolvedEnv : e.Source.Environment.Trim();

            var envelopeJson = JsonSerializer.Serialize(e, JsonOptions);

            docs.Add(new EconomicEventDocument
            {
                SchemaVersion = string.IsNullOrWhiteSpace(e.SchemaVersion) ? "1.0" : e.SchemaVersion.Trim(),
                EventId = e.EventId.Trim(),
                EventType = e.EventType.Trim(),
                EventVersion = e.EventVersion <= 0 ? 1 : e.EventVersion,
                OccurredAtUtc = e.OccurredAt.UtcDateTime,
                AppId = appId,
                Env = envelopeEnv,
                Producer = e.Source.Producer?.Trim() ?? string.Empty,
                Service = e.Source.Service?.Trim() ?? string.Empty,
                RequestId = e.Source.RequestId?.Trim(),
                Ip = e.Source.Ip?.Trim(),
                ActorType = string.IsNullOrWhiteSpace(e.Actor.ActorType) ? "system" : e.Actor.ActorType.Trim(),
                ActorId = e.Actor.ActorId?.Trim(),
                CampaignId = e.Correlation.CampaignId?.Trim(),
                RoundId = e.Correlation.RoundId?.Trim(),
                CommitmentId = e.Correlation.CommitmentId?.Trim(),
                AllocationId = e.Correlation.AllocationId?.Trim(),
                TransactionId = e.Correlation.TransactionId?.Trim(),
                UserId = e.Correlation.UserId?.Trim(),
                EnvelopeJson = envelopeJson,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _repo.InsertManyAsync(docs, cancellationToken);

        return Accepted(new { inserted = docs.Count });
    }

    private string GetDefaultEnv()
        => _env.IsDevelopment() ? "development" : "production";
}

public sealed record IngestEconomicEventsRequest(
    string AppId,
    string? Env,
    DateTimeOffset? SentAtUtc,
    IReadOnlyList<EconomicEventEnvelope> Events);
