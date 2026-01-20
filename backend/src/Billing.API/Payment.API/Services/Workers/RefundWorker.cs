using Payment.API.Infrastructure.Observability;
using System.Diagnostics;
using Payment.API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Payment.API.Services.Workers
{
    public class RefundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RefundWorker> _logger;
        private readonly ObservabilityMetrics _metrics;
        private readonly ObservabilityTracing _tracing;
        private readonly StructuredLogEmitter _logs;
        private readonly AnalyticsEventEmitter _analytics;
        private readonly ICorrelationContextAccessor _correlation;

        public RefundWorker(
            IServiceProvider serviceProvider,
            ILogger<RefundWorker> logger,
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
                using var span = _tracing.StartSpan("worker.refund.process", new ObservabilitySpanContext
                {
                    CorrelationId = correlationId
                });

                try
                {
                    _metrics.RecordRefundWorkerRun();
                    _logs.Info("Payment.RefundWorker.Started", new ActorContext(), new EntityContext(), new { correlationId });
                    _analytics.Emit("server.refund.worker_run", new ActorContext(), new EntityContext(), new { correlationId });

                    var stopwatch = Stopwatch.StartNew();

                    using var scope = _serviceProvider.CreateScope();
                    var transactionService = scope.ServiceProvider.GetRequiredService<TransactionService>();
                    var paymentService = scope.ServiceProvider.GetRequiredService<PaymentService>();

                    var pendingRefunds = await transactionService.GetPendingRefundsAsync();
                    _metrics.RefillPendingRefundsGauge(pendingRefunds.Count);
                    var actor = new ActorContext();

                    if (pendingRefunds.Count == 0)
                    {
                        _metrics.RecordRefundWorkerEmptyRun();
                        _logs.Info("Payment.RefundWorker.Idle", new ActorContext(), new EntityContext(), new { correlationId });
                        _analytics.Emit("server.refund.worker_idle", new ActorContext(), new EntityContext(), new { correlationId });
                    }
                    else
                    {
                        foreach (var refundTxn in pendingRefunds)
                        {
                            _correlation.CorrelationId = correlationId;
                            using var itemSpan = _tracing.StartSpan("worker.refund.process_item", new ObservabilitySpanContext
                            {
                                CorrelationId = correlationId,
                                PaymentIntentId = refundTxn.PaymentIntentId,
                                UserId = refundTxn.Metadata?.PayerUserId,
                                AppId = refundTxn.AppId
                            });

                            var entity = new EntityContext { PaymentIntentId = refundTxn.PaymentIntentId, RefundId = refundTxn.Id };
                            var transactionId = refundTxn.Id;
                            try
                            {
                                if (string.IsNullOrWhiteSpace(refundTxn.PaymentIntentId))
                                {
                                    if (!string.IsNullOrWhiteSpace(transactionId))
                                    {
                                        await transactionService.UpdateStatusAsync(transactionId, "RefundFailed");
                                    }
                                    _metrics.RecordRefundFailed();
                                    _logs.Warn("Payment.RefundWorker.InvalidPaymentIntent", actor, entity, new { correlationId, transactionId });
                                    _analytics.Emit("server.refund.worker_error", actor, entity, new { correlationId, transactionId, reason = "MissingPaymentIntentId" });
                                    continue;
                                }

                                var request = new Models.RefundRequest
                                {
                                    Amount = refundTxn.Amount,
                                    PaymentIntentId = refundTxn.PaymentIntentId,
                                    RefundedBy = "system-refund-worker",
                                    WalletId = refundTxn.Metadata?.PayerUserId ?? string.Empty,
                                    Reason = "auto_refund"
                                };

                                RefundResult? result = null;
                                var backoffs = new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };
                                for (var attempt = 0; attempt < backoffs.Length; attempt++)
                                {
                                    try
                                    {
                                        result = await paymentService.RefundPaymentAsync(request);
                                        if (result.Success)
                                        {
                                            if (!string.IsNullOrWhiteSpace(transactionId))
                                            {
                                                await transactionService.UpdateStatusAsync(transactionId, "Refunded");
                                            }
                                            else
                                            {
                                                _logs.Warn("Payment.RefundWorker.MissingTransactionId", actor, entity, new { correlationId, refundTxn.PaymentIntentId });
                                            }
                                            _logs.Info("Payment.RefundWorker.ItemSucceeded", actor, entity, new { correlationId, refundTxn.PaymentIntentId, result.RefundId, attempt = attempt + 1 });
                                            break;
                                        }

                                        if (attempt < backoffs.Length - 1)
                                        {
                                            _logs.Warn("Payment.RefundWorker.ItemRetry", actor, entity, new { correlationId, refundTxn.PaymentIntentId, attempt = attempt + 1, status = result.Status });
                                            await Task.Delay(backoffs[attempt], stoppingToken);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        if (attempt < backoffs.Length - 1)
                                        {
                                            _logs.Warn("Payment.RefundWorker.ItemRetry", actor, entity, new { correlationId, refundTxn.PaymentIntentId, attempt = attempt + 1, error = ex.Message });
                                            await Task.Delay(backoffs[attempt], stoppingToken);
                                            continue;
                                        }
                                        throw;
                                    }
                                }

                                if (result == null || !result.Success)
                                {
                                    if (!string.IsNullOrWhiteSpace(transactionId))
                                    {
                                        await transactionService.UpdateStatusAsync(transactionId, "RefundFailed");
                                    }
                                    _metrics.RecordRefundFailed();
                                    _logs.Warn("Payment.RefundWorker.ItemFailed", actor, entity, new { correlationId, refundTxn.PaymentIntentId, status = result?.Status });
                                    _analytics.Emit("server.refund.worker_error", actor, entity, new { correlationId, refundTxn.PaymentIntentId, status = result?.Status ?? "Unknown" });
                                }
                            }
                            catch (Exception ex)
                            {
                                if (!string.IsNullOrWhiteSpace(transactionId))
                                {
                                    await transactionService.UpdateStatusAsync(transactionId, "RefundFailed");
                                }
                                _metrics.RecordRefundFailed();
                                _logs.Error("Payment.RefundWorker.ItemError", actor, entity, new { correlationId, refundTxn.PaymentIntentId, error = ex.Message });
                                _analytics.Emit("server.refund.worker_error", actor, entity, new { correlationId, refundTxn.PaymentIntentId, error = ex.Message });
                            }
                        }

                        var remaining = await transactionService.CountPendingAsync("Refund");
                        _metrics.RefillPendingRefundsGauge(remaining);
                    }

                    stopwatch.Stop();
                    _metrics.RecordRefundProcessLatency(stopwatch.Elapsed);
                    _logs.Info("Payment.RefundWorker.Completed", new ActorContext(), new EntityContext(), new { correlationId });
                }
                catch (Exception ex)
                {
                    _metrics.RecordRefundWorkerError();
                    _logs.Error("Payment.RefundWorker.Error", new ActorContext(), new EntityContext(), new { error = ex.Message, type = ex.GetType().Name, correlationId });
                    _analytics.Emit("server.refund.worker_error", new ActorContext(), new EntityContext(), new 
                    { 
                        errorMessage = ex.Message, 
                        errorType = ex.GetType().Name,
                        correlationId 
                    });
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
