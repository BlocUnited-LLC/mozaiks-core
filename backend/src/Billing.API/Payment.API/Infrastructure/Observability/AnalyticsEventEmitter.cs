using System.Text.Json;

namespace Payment.API.Infrastructure.Observability
{
    public class AnalyticsEvent
    {
        public string EventName { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
        public string Ts { get; set; } = DateTime.UtcNow.ToString("O");
        public string Source { get; set; } = "server";
        public string CorrelationId { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? AppId { get; set; }
        public string? RoundId { get; set; }
        public string? InvestmentId { get; set; }
        public string? ProposalId { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? RefundId { get; set; }
        public object Payload { get; set; } = new { };
    }

    public class AnalyticsEventEmitter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly ILogger<AnalyticsEventEmitter> _logger;
        private readonly ICorrelationContextAccessor _correlation;

        public AnalyticsEventEmitter(ILogger<AnalyticsEventEmitter> logger, ICorrelationContextAccessor correlation)
        {
            _logger = logger;
            _correlation = correlation;
        }

        public void Emit(string eventName, ActorContext actor, EntityContext entity, object? payload = null)
        {
            var record = new AnalyticsEvent
            {
                EventName = eventName,
                Ts = DateTime.UtcNow.ToString("O"),
                CorrelationId = _correlation.CorrelationId,
                UserId = actor.UserId,
                AppId = actor.AppId,
                RoundId = entity.RoundId,
                InvestmentId = entity.InvestmentId,
                ProposalId = entity.ProposalId,
                PaymentIntentId = entity.PaymentIntentId,
                RefundId = entity.RefundId,
                Payload = payload ?? new { }
            };

            var json = JsonSerializer.Serialize(record, JsonOptions);
            _logger.LogInformation("{AnalyticsEvent}", json);
        }
    }
}
