using Payment.API.Infrastructure.Observability;
using System.Diagnostics;
using Payment.API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Payment.API.Services.Workers
{
    public class SettlementWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SettlementWorker> _logger;
        private readonly ObservabilityMetrics _metrics;
        private readonly ObservabilityTracing _tracing;
        private readonly StructuredLogEmitter _logs;
        private readonly AnalyticsEventEmitter _analytics;
        private readonly ICorrelationContextAccessor _correlation;

        public SettlementWorker(
            IServiceProvider serviceProvider,
            ILogger<SettlementWorker> logger,
            ObservabilityMetrics metrics,
            ObservabilityTracing tracing,
            StructuredLogEmitter logs,
            AnalyticsEventEmitter analytics,
            ICorrelationContextAccessor correlation)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _metrics = metrics;
            _tracing = tracing;
            _logs = logs;
            _analytics = analytics;
            _correlation = correlation;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var correlationId = Guid.NewGuid().ToString();
                _correlation.CorrelationId = correlationId;
                using var span = _tracing.StartSpan("worker.settlement.process", new ObservabilitySpanContext
                {
                    CorrelationId = correlationId
                });

                try
                {
                    _metrics.RecordSettlementWorkerRun();
                    _logs.Info("Payment.SettlementWorker.Started", new ActorContext(), new EntityContext(), new { correlationId });
                    _analytics.Emit("server.settlement.worker_run", new ActorContext(), new EntityContext(), new { correlationId });

                    var stopwatch = Stopwatch.StartNew();

                    using var scope = _serviceProvider.CreateScope();
                    var transactionService = scope.ServiceProvider.GetRequiredService<TransactionService>();
                    var settlementService = scope.ServiceProvider.GetRequiredService<SettlementService>();

                    var pendingSettlements = await transactionService.GetPendingSettlementsAsync();
                    _metrics.RefillPendingSettlementsGauge(pendingSettlements.Count);
                    var actor = new ActorContext();

                    if (pendingSettlements.Count == 0)
                    {
                        _metrics.RecordSettlementWorkerEmptyRun();
                        _logs.Info("Payment.SettlementWorker.Idle", new ActorContext(), new EntityContext(), new { correlationId });
                        _analytics.Emit("server.settlement.worker_idle", new ActorContext(), new EntityContext(), new { correlationId });
                    }
                    else
                    {
                        foreach (var settlementTxn in pendingSettlements)
                        {
                            _correlation.CorrelationId = correlationId;
                            using var itemSpan = _tracing.StartSpan("worker.settlement.process_item", new ObservabilitySpanContext
                            {
                                CorrelationId = correlationId,
                                UserId = settlementTxn.Metadata?.PayerUserId,
                                AppId = settlementTxn.AppId,
                                RoundId = settlementTxn.Metadata?.SubscriptionId
                            });

                            var destinationAccountId = settlementTxn.Metadata?.AppCreatorId ?? string.Empty;
                            var entity = new EntityContext { AppId = settlementTxn.AppId, RoundId = settlementTxn.Metadata?.SubscriptionId };
                            var transactionId = settlementTxn.Id;
                            actor = new ActorContext { UserId = settlementTxn.Metadata?.PayerUserId, AppId = settlementTxn.AppId };

                            try
                            {
                                if (string.IsNullOrWhiteSpace(destinationAccountId))
                                {
                                    if (!string.IsNullOrWhiteSpace(transactionId))
                                    {
                                        await transactionService.UpdateStatusAsync(transactionId, "SettlementFailed");
                                    }
                                    _metrics.RecordSettlementFailed();
                                    _metrics.RecordSettlementInvalidDestination("MissingDestinationAccountId");
                                    _logs.Warn("Payment.SettlementWorker.InvalidDestination", actor, entity, new { correlationId, transactionId });
                                    _analytics.Emit("server.settlement.worker_error", actor, entity, new { correlationId, transactionId, reason = "MissingDestinationAccountId" });
                                    continue;
                                }

                                var amount = Convert.ToDecimal(settlementTxn.Amount) / 100m;
                                var backoffs = new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };
                                    for (var attempt = 0; attempt < backoffs.Length; attempt++)
                                    {
                                        try
                                        {
                                            await settlementService.ProcessSettlementAsync(settlementTxn.AppId, destinationAccountId, amount);
                                            if (!string.IsNullOrWhiteSpace(transactionId))
                                            {
                                                await transactionService.UpdateStatusAsync(transactionId, "Settled");
                                            }
                                        else
                                        {
                                            _logs.Warn("Payment.SettlementWorker.MissingTransactionId", actor, entity, new { correlationId });
                                        }
                                        _logs.Info("Payment.SettlementWorker.ItemSucceeded", actor, entity, new { correlationId, settlementTxn.TransactionType, settlementTxn.Amount, attempt = attempt + 1 });
                                        break;
                                    }
                                    catch (SettlementValidationException vex)
                                    {
                                        if (!string.IsNullOrWhiteSpace(transactionId))
                                        {
                                            await transactionService.UpdateStatusAsync(transactionId, "SettlementFailed");
                                        }

                                        _metrics.RecordSettlementFailed();
                                        if (vex.Reason.Contains("DestinationAccountId", StringComparison.OrdinalIgnoreCase))
                                        {
                                            _metrics.RecordSettlementInvalidDestination(vex.Reason);
                                        }

                                        _logs.Warn(
                                            "Payment.SettlementWorker.NonRetryable",
                                            actor,
                                            entity,
                                            new { correlationId, reason = vex.Reason, error = vex.Message, destinationAccountId });

                                        _analytics.Emit(
                                            "server.settlement.worker_error",
                                            actor,
                                            entity,
                                            new { correlationId, reason = vex.Reason, error = vex.Message });

                                        break;
                                    }
                                    catch (Exception ex)
                                    {
                                        if (attempt < backoffs.Length - 1)
                                        {
                                            _logs.Warn("Payment.SettlementWorker.ItemRetry", actor, entity, new { correlationId, attempt = attempt + 1, error = ex.Message });
                                            await Task.Delay(backoffs[attempt], stoppingToken);
                                            continue;
                                        }

                                        if (!string.IsNullOrWhiteSpace(transactionId))
                                        {
                                            await transactionService.UpdateStatusAsync(transactionId, "SettlementFailed");
                                        }
                                        _metrics.RecordSettlementFailed();
                                        _logs.Error("Payment.SettlementWorker.ItemError", actor, entity, new { correlationId, error = ex.Message });
                                        _analytics.Emit("server.settlement.worker_error", actor, entity, new { correlationId, error = ex.Message });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                if (!string.IsNullOrWhiteSpace(transactionId))
                                {
                                    await transactionService.UpdateStatusAsync(transactionId, "SettlementFailed");
                                }
                                _metrics.RecordSettlementFailed();
                                _logs.Error("Payment.SettlementWorker.ItemError", actor, entity, new { correlationId, error = ex.Message });
                                _analytics.Emit("server.settlement.worker_error", actor, entity, new { correlationId, error = ex.Message });
                            }
                        }

                        var remaining = await transactionService.CountPendingAsync("Settlement");
                        _metrics.RefillPendingSettlementsGauge(remaining);
                    }

                    stopwatch.Stop();
                    _metrics.RecordSettlementProcessLatency(stopwatch.Elapsed);
                    _logs.Info("Payment.SettlementWorker.Completed", new ActorContext(), new EntityContext(), new { correlationId });
                }
                catch (Exception ex)
                {
                    _metrics.RecordSettlementWorkerError();
                    _logs.Error("Payment.SettlementWorker.Error", new ActorContext(), new EntityContext(), new { error = ex.Message, type = ex.GetType().Name, correlationId });
                    _analytics.Emit("server.settlement.worker_error", new ActorContext(), new EntityContext(), new 
                    { 
                        errorMessage = ex.Message, 
                        errorType = ex.GetType().Name,
                        correlationId 
                    });
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
