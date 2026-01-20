using Payment.API.Infrastructure.Observability;
using System.Diagnostics;
using Stripe;

namespace Payment.API.Services
{
    public sealed class SettlementValidationException : Exception
    {
        public string Reason { get; }

        public SettlementValidationException(string reason, string message) : base(message)
        {
            Reason = reason;
        }
    }

    public class SettlementService
    {
        private readonly StructuredLogEmitter _logs;
        private readonly ObservabilityMetrics _metrics;
        private readonly ObservabilityTracing _tracing;
        private readonly AnalyticsEventEmitter _analytics;
        private readonly ICorrelationContextAccessor _correlation;
        private readonly TransferService _transferService;
        private readonly bool _enforceDestinationAccountFormat;
        private readonly string _destinationAccountIdPrefix;

        public SettlementService(
            StructuredLogEmitter logs,
            ObservabilityMetrics metrics,
            ObservabilityTracing tracing,
            AnalyticsEventEmitter analytics,
            ICorrelationContextAccessor correlation,
            IConfiguration configuration)
        {
            _logs = logs;
            _metrics = metrics;
            _tracing = tracing;
            _analytics = analytics;
            _correlation = correlation;

            var secretKey = configuration.GetValue<string>("Payments:SecretKey");
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException("Payments secret key is not configured.");
            }

            StripeConfiguration.ApiKey = secretKey;
            _transferService = new TransferService();

            _enforceDestinationAccountFormat = configuration.GetValue("Settlement:EnforceDestinationAccountFormat", true);
            _destinationAccountIdPrefix = configuration.GetValue<string>("Settlement:DestinationAccountIdPrefix") ?? "acct_";
        }

        public async Task ProcessSettlementAsync(string appId, string destinationAccountId, decimal amount)
        {
            using var span = _tracing.StartSpan("worker.settlement.process", new ObservabilitySpanContext
            {
                AppId = appId,
                CorrelationId = _correlation.CorrelationId
            });

            _metrics.RecordSettlementStarted();
            _logs.Info("Settlement.Started", new ActorContext { AppId = appId }, new EntityContext { AppId = appId }, new { amount, destinationAccountId });

            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (string.IsNullOrWhiteSpace(destinationAccountId))
                {
                    throw new SettlementValidationException("MissingDestinationAccountId", "DestinationAccountId is required for settlement.");
                }

                if (_enforceDestinationAccountFormat && !destinationAccountId.StartsWith(_destinationAccountIdPrefix, StringComparison.Ordinal))
                {
                    throw new SettlementValidationException(
                        "InvalidDestinationAccountIdFormat",
                        $"DestinationAccountId must start with '{_destinationAccountIdPrefix}'.");
                }

                if (amount <= 0)
                {
                    throw new SettlementValidationException("InvalidAmount", "Settlement amount must be > 0.");
                }

                var amountInCents = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

                var transfer = await _transferService.CreateAsync(new TransferCreateOptions
                {
                    Amount = amountInCents,
                    Currency = "usd",
                    Destination = destinationAccountId,
                    Metadata = new Dictionary<string, string>
                    {
                        ["correlationId"] = _correlation.CorrelationId
                    }
                });

                stopwatch.Stop();
                _metrics.RecordSettlementProcessLatency(stopwatch.Elapsed);
                _metrics.RecordSettlementCompleted();

                _logs.Info("Settlement.Completed", new ActorContext { AppId = appId }, new EntityContext { AppId = appId }, new { amount, transferId = transfer.Id, destinationAccountId });
                _analytics.Emit("server.settlement.completed", new ActorContext { AppId = appId }, new EntityContext { AppId = appId }, new { amount, transferId = transfer.Id });
            }
            catch (StripeException ex)
            {
                stopwatch.Stop();
                _metrics.RecordSettlementFailed();

                var reason = ex.StripeError?.Code ?? ex.StripeError?.Type ?? "ProviderError";
                _logs.Error("Settlement.Failed", new ActorContext { AppId = appId }, new EntityContext { AppId = appId }, new { reason, destinationAccountId });
                _analytics.Emit("server.settlement.failed", new ActorContext { AppId = appId }, new EntityContext { AppId = appId }, new { reason });

                // Prevent provider exception types/messages from bubbling to shell output.
                throw new Exception("Settlement failed.");
            }
            catch (Exception)
            {
                stopwatch.Stop();
                _metrics.RecordSettlementFailed();
                _logs.Error("Settlement.Failed", new ActorContext { AppId = appId }, new EntityContext { AppId = appId }, new { reason = "UnexpectedError", destinationAccountId });
                _analytics.Emit("server.settlement.failed", new ActorContext { AppId = appId }, new EntityContext { AppId = appId }, new { reason = "UnexpectedError" });
                throw;
            }
        }
    }
}
