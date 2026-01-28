using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Payment.API.Infrastructure.Observability
{
    public class ActorContext
    {
        public string? UserId { get; set; }
        public string? AppId { get; set; }
    }

    public class EntityContext
    {
        public string? RoundId { get; set; }
        public string? InvestmentId { get; set; }
        public string? ProposalId { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? RefundId { get; set; }
        public string? AppId { get; set; }
    }

    public class StructuredLogContext
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? AppId { get; set; }
        public string? ProposalId { get; set; }
        public string? RoundId { get; set; }
    }

    public class StructuredLogRecord
    {
        public string Ts { get; set; } = DateTime.UtcNow.ToString("O");
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
        public StructuredLogContext Context { get; set; } = new();
        public object Payload { get; set; } = new { };
    }

    public class StructuredLogEmitter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly ILogger<StructuredLogEmitter> _logger;
        private readonly ICorrelationContextAccessor _correlation;

        public StructuredLogEmitter(ILogger<StructuredLogEmitter> logger, ICorrelationContextAccessor correlation, IConfiguration configuration)
        {
            _logger = logger;
            _correlation = correlation;
            _ = configuration;
        }

        public void Info(string eventName, ActorContext actor, EntityContext entity, object? payload = null, string? serviceOverride = null)
            => Write(LogLevel.Information, "INFO", eventName, actor, entity, payload, serviceOverride);

        public void Debug(string eventName, ActorContext actor, EntityContext entity, object? payload = null, string? serviceOverride = null)
            => Write(LogLevel.Debug, "DEBUG", eventName, actor, entity, payload, serviceOverride);

        public void Warn(string eventName, ActorContext actor, EntityContext entity, object? payload = null, string? serviceOverride = null)
            => Write(LogLevel.Warning, "WARN", eventName, actor, entity, payload, serviceOverride);

        public void Error(string eventName, ActorContext actor, EntityContext entity, object? payload = null, string? serviceOverride = null)
            => Write(LogLevel.Error, "ERROR", eventName, actor, entity, payload, serviceOverride);

        private void Write(LogLevel level, string label, string eventName, ActorContext actor, EntityContext entity, object? payload, string? serviceOverride)
        {
            _ = serviceOverride;
            var correlationId = _correlation.CorrelationId;
            var record = new StructuredLogRecord
            {
                Ts = DateTime.UtcNow.ToString("O"),
                Level = label,
                Message = eventName,
                Context = new StructuredLogContext
                {
                    CorrelationId = correlationId,
                    UserId = actor.UserId,
                    AppId = actor.AppId ?? entity.AppId,
                    ProposalId = entity.ProposalId,
                    RoundId = entity.RoundId
                },
                Payload = payload ?? new { }
            };

            var json = JsonSerializer.Serialize(record, JsonOptions);
            _logger.Log(level, "{StructuredLog}", json);
        }
    }
}
