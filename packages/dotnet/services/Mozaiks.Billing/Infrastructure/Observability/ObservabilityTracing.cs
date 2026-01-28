using System.Diagnostics;

namespace Payment.API.Infrastructure.Observability
{
    public class ObservabilitySpanContext
    {
        public string? CorrelationId { get; set; }
        public string? UserId { get; set; }
        public string? AppId { get; set; }
        public string? RoundId { get; set; }
        public string? InvestmentId { get; set; }
        public string? ProposalId { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? RefundId { get; set; }
    }

    public class ObservabilityTracing
    {
        private readonly ActivitySource _source = new("mozaiks.payment-api");
        private readonly ICorrelationContextAccessor _correlation;

        public ObservabilityTracing(ICorrelationContextAccessor correlation)
        {
            _correlation = correlation;
        }

        public Activity? StartSpan(string name, ObservabilitySpanContext context)
        {
            var activity = _source.StartActivity(name, ActivityKind.Internal);
            if (activity != null)
            {
                var correlationId = context.CorrelationId ?? _correlation.CorrelationId ?? string.Empty;
                activity.SetTag("correlationId", correlationId);
                activity.SetTag("userId", context.UserId ?? string.Empty);
                activity.SetTag("appId", context.AppId ?? string.Empty);
                activity.SetTag("roundId", context.RoundId ?? string.Empty);
                activity.SetTag("investmentId", context.InvestmentId ?? string.Empty);
                activity.SetTag("proposalId", context.ProposalId ?? string.Empty);
                activity.SetTag("paymentIntentId", context.PaymentIntentId ?? string.Empty);
                activity.SetTag("refundId", context.RefundId ?? string.Empty);
                activity.SetTag("snapshotVersion", 0);
                activity.SetTag("voteWeight", 0);
                activity.SetTag("totalVotingPower", 0);
            }

            return activity;
        }
    }
}
