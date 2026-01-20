using System.Text.Json;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using MongoDB.Bson;

namespace AuthServer.Api.Services;

public sealed class AppMonetizationAuditService
{
    private readonly IAppMonetizationAuditRepository _repository;
    private readonly ILogger<AppMonetizationAuditService> _logger;

    public AppMonetizationAuditService(
        IAppMonetizationAuditRepository repository,
        ILogger<AppMonetizationAuditService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task TryWriteAsync(
        string appId,
        string actorUserId,
        string eventType,
        string correlationId,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = BsonDocument.Parse(JsonSerializer.Serialize(payload));
            var auditEvent = new AppMonetizationAuditEvent
            {
                AppId = appId,
                ActorUserId = actorUserId,
                EventType = eventType,
                CorrelationId = correlationId,
                OccurredAtUtc = DateTime.UtcNow,
                Payload = document
            };

            await _repository.InsertAsync(auditEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write monetization audit event {EventType} for app {AppId}", eventType, appId);
        }
    }
}
