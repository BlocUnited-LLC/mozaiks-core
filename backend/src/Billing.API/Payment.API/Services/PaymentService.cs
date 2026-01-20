using Payment.API.Infrastructure.Observability;
using Payment.API.Models;
using Payment.API.Repository.Interfaces;
using Stripe;
using System.Diagnostics;
using System.Text.Json;
using EventBus.Messages.EconomicProtocol;

namespace Payment.API.Services
{
    /// <summary>
    /// Core payment orchestration service for fiat transactions via Stripe.
    /// Handles payment intents, refunds, and webhook event processing.
    /// 
    /// BLOCKCHAIN INTEGRATION ARCHITECTURE:
    /// This is THE central point where blockchain payments would be integrated.
    /// 
    /// Recommended approach:
    /// 1. Create IPaymentProcessor interface:
    ///    - CreatePaymentAsync(amount, currency, metadata) → PaymentResult
    ///    - RefundAsync(transactionId, amount) → RefundResult  
    ///    - GetStatusAsync(transactionId) → PaymentStatus
    /// 
    /// 2. Implementations:
    ///    - StripePaymentProcessor (extract current Stripe logic)
    ///    - EthereumPaymentProcessor (Web3.js/ethers.js via interop or REST)
    ///    - SolanaPaymentProcessor (Solana Pay integration)
    /// 
    /// 3. PaymentService becomes the router:
    ///    - Inspects request.Currency or request.PaymentMethod
    ///    - Routes to appropriate processor
    ///    - Unified response/ledger recording
    /// 
    /// 4. Webhook handling:
    ///    - Stripe webhooks (current)
    ///    - Blockchain event listeners (new) - poll or WebSocket for confirmations
    /// </summary>
    public class PaymentService
    {
        private readonly StructuredLogEmitter _logs;
        private readonly ObservabilityMetrics _metrics;
        private readonly ObservabilityTracing _tracing;
        private readonly AnalyticsEventEmitter _analytics;
        private readonly ICorrelationContextAccessor _correlation;
        private readonly PaymentIntentService _paymentIntentService;
        private readonly RefundService _refundService;
        private readonly TransactionService _transactionService;
        private readonly IWalletRepository _walletRepository;
        private readonly LedgerService _ledgerService;
        private readonly EconomicEventAppender _economicEvents;
        
        // BLOCKCHAIN: Add payment processor abstraction
        // private readonly IEnumerable<IPaymentProcessor> _processors;

        public PaymentService(
            StructuredLogEmitter logs,
            ObservabilityMetrics metrics,
            ObservabilityTracing tracing,
            AnalyticsEventEmitter analytics,
            ICorrelationContextAccessor correlation,
            TransactionService transactionService,
            IWalletRepository walletRepository,
            LedgerService ledgerService,
            EconomicEventAppender economicEvents,
            IConfiguration configuration)
        {
            _logs = logs;
            _metrics = metrics;
            _tracing = tracing;
            _analytics = analytics;
            _correlation = correlation;
            _transactionService = transactionService;
            _walletRepository = walletRepository;
            _ledgerService = ledgerService;
            _economicEvents = economicEvents;

            // BLOCKCHAIN: Move this Stripe-specific init into StripePaymentProcessor
            var secretKey = configuration.GetValue<string>("Payments:SecretKey");
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException("Payments secret key is not configured.");
            }

            StripeConfiguration.ApiKey = secretKey;
            _paymentIntentService = new PaymentIntentService();
            _refundService = new RefundService();
        }

        /// <summary>
        /// Creates a payment intent for fiat card payments.
        /// BLOCKCHAIN: Extend to accept currency type and route to appropriate processor.
        /// For crypto: Generate payment address or Solana Pay URL instead of client secret.
        /// </summary>
        public async Task<PaymentIntentResult> CreatePaymentIntentAsync(PaymentIntentRequest request)
        {
            using var span = _tracing.StartSpan("payment.intent_create", new ObservabilitySpanContext
            {
                UserId = request.UserId,
                AppId = request.AppId,
                RoundId = request.RoundId,
                InvestmentId = request.InvestmentId
            });

            var actor = new ActorContext { UserId = request.UserId, AppId = request.AppId };
            var entity = new EntityContext { RoundId = request.RoundId, InvestmentId = request.InvestmentId, AppId = request.AppId };

            var metadata = new Dictionary<string, string>
            {
                { "userId", request.UserId },
                { "appId", request.AppId },
                { "roundId", request.RoundId ?? string.Empty },
                { "investmentId", request.InvestmentId ?? string.Empty },
                { "correlationId", _correlation.CorrelationId }
            };

            var options = new PaymentIntentCreateOptions
            {
                Amount = request.Amount,
                Currency = request.Currency,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                Metadata = metadata
            };

            if (!string.IsNullOrWhiteSpace(request.DestinationAccountId))
            {
                options.TransferData = new PaymentIntentTransferDataOptions
                {
                    Destination = request.DestinationAccountId
                };

                if (request.ApplicationFeeAmount.HasValue)
                {
                    options.ApplicationFeeAmount = request.ApplicationFeeAmount;
                }
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var intent = await _paymentIntentService.CreateAsync(options);
                stopwatch.Stop();

                _metrics.RecordPaymentIntentLatency(stopwatch.Elapsed);
                _metrics.RecordPaymentIntentCreated();

                _logs.Info("Payment.IntentCreated", actor, new EntityContext
                {
                    PaymentIntentId = intent.Id,
                    RoundId = request.RoundId,
                    InvestmentId = request.InvestmentId,
                    AppId = request.AppId
                }, new
                {
                    request.Amount,
                    request.Currency,
                    request.DestinationAccountId,
                    request.ApplicationFeeAmount
                });

                _analytics.Emit("server.payment.intent_created", actor, new EntityContext
                {
                    PaymentIntentId = intent.Id,
                    RoundId = request.RoundId,
                    InvestmentId = request.InvestmentId,
                    AppId = request.AppId
                }, new
                {
                    request.Amount,
                    request.Currency,
                    request.DestinationAccountId,
                    request.ApplicationFeeAmount
                });

                // Persist a local transaction record for status/history/ledger linkage.
                var transactionType = string.IsNullOrWhiteSpace(request.TransactionType) ? "Payment" : request.TransactionType;
                var localTxn = new TransactionModel
                {
                    TransactionType = transactionType,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    PaymentIntentId = intent.Id,
                    WalletId = request.WalletId ?? string.Empty,
                    AppId = request.AppId ?? string.Empty,
                    Status = "Pending",
                    Metadata = new TransactionMetadata
                    {
                        PayerUserId = request.UserId ?? string.Empty,
                        // Existing workers treat AppCreatorId as the destination account id.
                        AppCreatorId = request.DestinationAccountId ?? string.Empty,
                        // Existing workers use SubscriptionId as RoundId context.
                        SubscriptionId = request.SubscriptionId ?? request.RoundId ?? string.Empty,
                        InvestorShares = new()
                    }
                };

                await _transactionService.CreateTransactionAsync(localTxn);

                // Record the transaction in the wallet history (no balance change yet until success/refund).
                if (!string.IsNullOrWhiteSpace(localTxn.WalletId))
                {
                    await _walletRepository.AddTransactionAsync(localTxn.WalletId, new WalletTransaction
                    {
                        TransactionId = localTxn.Id ?? string.Empty,
                        PaymentIntentId = localTxn.PaymentIntentId,
                        Amount = localTxn.Amount,
                        Currency = localTxn.Currency,
                        TransactionType = localTxn.TransactionType,
                        Status = localTxn.Status
                    });
                }

                return new PaymentIntentResult
                {
                    Success = true,
                    PaymentIntentId = intent.Id,
                    ClientSecret = intent.ClientSecret
                };
            }
            catch (StripeException ex)
            {
                var reason = ex.StripeError?.Code ?? ex.StripeError?.Type ?? "ProviderError";
                _metrics.RecordPaymentConfirmFailed(reason);
                _logs.Error("Payment.Failed", actor, entity, new { reason });
                _analytics.Emit("server.payment.failed", actor, entity, new { reason });
                return new PaymentIntentResult { Success = false, ErrorReason = reason };
            }
            catch (Exception)
            {
                _metrics.RecordPaymentConfirmFailed("UnexpectedError");
                _logs.Error("Payment.Failed", actor, entity, new { reason = "UnexpectedError" });
                _analytics.Emit("server.payment.failed", actor, entity, new { reason = "UnexpectedError" });
                return new PaymentIntentResult { Success = false, ErrorReason = "UnexpectedError" };
            }
        }

        public async Task<PaymentConfirmResult> ConfirmPaymentIntentAsync(PaymentConfirmRequest request)
        {
            using var span = _tracing.StartSpan("payment.intent_confirm", new ObservabilitySpanContext
            {
                PaymentIntentId = request.PaymentIntentId,
                UserId = request.UserId,
                AppId = request.AppId,
                RoundId = request.RoundId,
                InvestmentId = request.InvestmentId
            });

            var actor = new ActorContext { UserId = request.UserId, AppId = request.AppId };
            var entity = new EntityContext
            {
                PaymentIntentId = request.PaymentIntentId,
                RoundId = request.RoundId,
                InvestmentId = request.InvestmentId,
                AppId = request.AppId
            };

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var intent = await _paymentIntentService.GetAsync(request.PaymentIntentId);

                if (intent.Status is "requires_confirmation" or "requires_action")
                {
                    intent = await _paymentIntentService.ConfirmAsync(request.PaymentIntentId);
                }
                stopwatch.Stop();
                _metrics.RecordPaymentConfirmLatency(stopwatch.Elapsed);

                if (intent.Status == "succeeded")
                {
                    var amountReceived = intent.AmountReceived;
                    if (amountReceived <= 0)
                    {
                        amountReceived = intent.Amount;
                    }
                    await ApplyPaymentIntentStatusAsync(intent.Id, "Succeeded", amountDeltaOverride: amountReceived, providerEventId: null);
                    _logs.Info("Payment.Confirmed", actor, entity, new { status = intent.Status });
                    _analytics.Emit("server.payment.confirmed", actor, entity, new { status = intent.Status });
                    return new PaymentConfirmResult { Success = true, Status = intent.Status, PaymentIntentId = intent.Id };
                }

                if (intent.Status is "canceled" or "requires_payment_method")
                {
                    await ApplyPaymentIntentStatusAsync(intent.Id, "Failed", amountDeltaOverride: 0, providerEventId: null);
                }

                _metrics.RecordPaymentConfirmFailed(intent.Status ?? "unknown_status");
                _logs.Warn("Payment.Failed", actor, entity, new { status = intent.Status });
                _analytics.Emit("server.payment.failed", actor, entity, new { status = intent.Status });
                return new PaymentConfirmResult { Success = false, Status = intent.Status ?? "failed", PaymentIntentId = intent.Id };
            }
            catch (StripeException ex)
            {
                var reason = ex.StripeError?.Code ?? ex.StripeError?.Type ?? "ProviderError";
                _metrics.RecordPaymentConfirmFailed(reason);
                _logs.Error("Payment.Failed", actor, entity, new { reason });
                _analytics.Emit("server.payment.failed", actor, entity, new { reason });
                return new PaymentConfirmResult { Success = false, Status = reason, PaymentIntentId = request.PaymentIntentId };
            }
            catch (Exception)
            {
                _metrics.RecordPaymentConfirmFailed("UnexpectedError");
                _logs.Error("Payment.Failed", actor, entity, new { reason = "UnexpectedError" });
                _analytics.Emit("server.payment.failed", actor, entity, new { reason = "UnexpectedError" });
                return new PaymentConfirmResult { Success = false, Status = "UnexpectedError", PaymentIntentId = request.PaymentIntentId };
            }
        }

        public async Task<RefundResult> RefundPaymentAsync(RefundRequest request)
        {
            using var span = _tracing.StartSpan("payment.refund_create", new ObservabilitySpanContext
            {
                PaymentIntentId = request.PaymentIntentId,
                UserId = request.RefundedBy
            });

            var actor = new ActorContext { UserId = request.RefundedBy };
            var refundRequestId = Guid.NewGuid().ToString();
            var entity = new EntityContext { PaymentIntentId = request.PaymentIntentId, RefundId = refundRequestId };

            _metrics.RecordRefundRequested();
            _logs.Info("Refund.Requested", actor, entity, new { request.Amount, request.Reason });
            _analytics.Emit("server.refund.requested", actor, entity, new { request.Amount, request.Reason });

            try
            {
                _metrics.RecordRefundInProgress();
                var stopwatch = Stopwatch.StartNew();

                var options = new RefundCreateOptions
                {
                    PaymentIntent = request.PaymentIntentId,
                    Amount = request.Amount > 0 ? request.Amount : null,
                    Reason = request.Reason,
                    Metadata = new Dictionary<string, string>
                    {
                        { "refundedBy", request.RefundedBy },
                        { "paymentIntentId", request.PaymentIntentId },
                        { "correlationId", _correlation.CorrelationId },
                        { "refundRequestId", refundRequestId }
                    }
                };

                var refund = await _refundService.CreateAsync(options);
                stopwatch.Stop();

                _metrics.RecordRefundProcessLatency(stopwatch.Elapsed);
                _metrics.RecordRefundCompleted();
                _metrics.RecordWalletRefundApplied();
                _logs.Info("Wallet.RefundApplied", actor, new EntityContext { PaymentIntentId = request.PaymentIntentId, RefundId = refund.Id }, new { refund.Id, refund.Amount });
                _analytics.Emit("server.wallet.refund_applied", actor, new EntityContext { PaymentIntentId = request.PaymentIntentId, RefundId = refund.Id }, new { refund.Id, refund.Amount });
                _logs.Info("Refund.Completed", actor, new EntityContext { PaymentIntentId = request.PaymentIntentId, RefundId = refund.Id }, new { refund.Id, refund.Status, refund.Amount });
                _analytics.Emit("server.refund.completed", actor, new EntityContext { PaymentIntentId = request.PaymentIntentId, RefundId = refund.Id }, new { refund.Id, refund.Status, refund.Amount });

                // Reflect refund locally (best-effort; webhooks may also apply this).
                if (refund.Status is "succeeded" or "pending")
                {
                    var refundAmount = refund.Amount;
                    if (refundAmount <= 0)
                    {
                        refundAmount = request.Amount;
                    }
                    await ApplyPaymentIntentStatusAsync(request.PaymentIntentId, "Refunded", amountDeltaOverride: -refundAmount, providerEventId: null);
                }

                return new RefundResult { Success = true, RefundId = refund.Id, Status = refund.Status };
            }
            catch (StripeException ex)
            {
                var reason = ex.StripeError?.Code ?? ex.StripeError?.Type ?? "ProviderError";
                _metrics.RecordRefundFailed();
                _logs.Error("Refund.Failed", actor, entity, new { reason });
                _analytics.Emit("server.refund.failed", actor, entity, new { reason });
                return new RefundResult { Success = false, Status = reason };
            }
            catch (Exception)
            {
                _metrics.RecordRefundFailed();
                _logs.Error("Refund.Failed", actor, entity, new { reason = "UnexpectedError" });
                _analytics.Emit("server.refund.failed", actor, entity, new { reason = "UnexpectedError" });
                return new RefundResult { Success = false, Status = "UnexpectedError" };
            }
        }

        public async Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentIntentId)
        {
            var providerStatus = "unknown";
            try
            {
                var intent = await _paymentIntentService.GetAsync(paymentIntentId);
                providerStatus = intent.Status ?? "unknown";
            }
            catch
            {
                // Keep providerStatus as unknown; local may still know something.
            }

            var local = await _transactionService.GetByIntentIdAsync(paymentIntentId);
            return new PaymentStatusResult
            {
                PaymentIntentId = paymentIntentId,
                ProviderStatus = providerStatus,
                LocalStatus = local?.Status,
                WalletId = local?.WalletId,
                Amount = local?.Amount,
                Currency = local?.Currency
            };
        }

        public async Task HandleWebhookEventAsync(Event providerEvent)
        {
            if (providerEvent == null) return;

            switch (providerEvent.Type)
            {
                case "payment_intent.succeeded":
                {
                    if (providerEvent.Data.Object is PaymentIntent pi)
                    {
                        var amountReceived = pi.AmountReceived;
                        if (amountReceived <= 0)
                        {
                            amountReceived = pi.Amount;
                        }
                        await ApplyPaymentIntentStatusAsync(
                            pi.Id,
                            "Succeeded",
                            amountDeltaOverride: amountReceived,
                            providerEventId: providerEvent.Id);
                    }
                    break;
                }
                case "payment_intent.payment_failed":
                {
                    if (providerEvent.Data.Object is PaymentIntent pi)
                    {
                        await ApplyPaymentIntentStatusAsync(pi.Id, "Failed", amountDeltaOverride: 0, providerEventId: providerEvent.Id);
                    }
                    break;
                }
                case "payment_intent.canceled":
                {
                    if (providerEvent.Data.Object is PaymentIntent pi)
                    {
                        await ApplyPaymentIntentStatusAsync(pi.Id, "Canceled", amountDeltaOverride: 0, providerEventId: providerEvent.Id);
                    }
                    break;
                }
                case "charge.refunded":
                {
                    if (providerEvent.Data.Object is Charge charge)
                    {
                        var paymentIntentId = charge.PaymentIntentId;
                        if (!string.IsNullOrWhiteSpace(paymentIntentId))
                        {
                            var refunded = charge.AmountRefunded;
                            if (refunded <= 0)
                            {
                                refunded = charge.Amount;
                            }
                            var delta = -refunded;
                            await ApplyPaymentIntentStatusAsync(paymentIntentId, "Refunded", amountDeltaOverride: delta, providerEventId: providerEvent.Id);
                        }
                    }
                    break;
                }
            }
        }

        private async Task ApplyPaymentIntentStatusAsync(string paymentIntentId, string status, long? amountDeltaOverride, string? providerEventId)
        {
            if (string.IsNullOrWhiteSpace(paymentIntentId)) return;

            var txn = await _transactionService.GetByIntentIdAsync(paymentIntentId);
            if (txn == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(txn.Id))
            {
                await _transactionService.UpdateStatusAsync(txn.Id, status);
            }

            var walletId = txn.WalletId;
            var delta = amountDeltaOverride ?? 0;

            if (!string.IsNullOrWhiteSpace(walletId))
            {
                await _walletRepository.UpdateWalletBalanceAndTransactionStatusAsync(walletId, paymentIntentId, delta, status);

                // Ledger entry to keep a coherent audit trail.
                var entryType = status switch
                {
                    "Succeeded" => "Credit",
                    "Refunded" => "Refund",
                    "Failed" => "Error",
                    "Canceled" => "Error",
                    _ => "System"
                };

                await _ledgerService.RecordEntryAsync(new LedgerEntryModel
                {
                    UserId = txn.Metadata?.PayerUserId ?? string.Empty,
                    AppId = txn.AppId ?? string.Empty,
                    WalletId = walletId,
                    TransactionId = txn.Id ?? string.Empty,
                    PaymentIntentId = paymentIntentId,
                    Type = entryType,
                    Source = "PaymentProcessor",
                    Reason = status,
                    Amount = Math.Abs(delta),
                    Currency = txn.Currency ?? "usd"
                });
            }

            _analytics.Emit(
                "server.payment.status_applied",
                new ActorContext { UserId = txn.Metadata?.PayerUserId, AppId = txn.AppId },
                new EntityContext { PaymentIntentId = paymentIntentId, AppId = txn.AppId, RoundId = txn.Metadata?.SubscriptionId },
                new { status, amountDelta = delta });

            await TryEmitRevenueEconomicEventAsync(txn, paymentIntentId, status, delta, providerEventId);
        }

        private async Task TryEmitRevenueEconomicEventAsync(
            TransactionModel txn,
            string paymentIntentId,
            string status,
            long amountDelta,
            string? providerEventId)
        {
            // Only emit on provider webhooks to avoid duplication (client-side confirm + webhook).
            if (string.IsNullOrWhiteSpace(providerEventId))
            {
                return;
            }

            if (!IsMozaiksPayOneTimeRevenue(txn))
            {
                return;
            }

            var userId = txn.Metadata?.PayerUserId;
            var appId = string.IsNullOrWhiteSpace(txn.AppId) ? "platform" : txn.AppId;

            var economicType = status switch
            {
                "Succeeded" => EconomicEventTypes.RevenueInvoicePaid,
                "Refunded" => EconomicEventTypes.RevenueRefundIssued,
                _ => null
            };

            if (economicType == null)
            {
                return;
            }

            try
            {
                object payload = status switch
                {
                    "Succeeded" => (object)new
                    {
                        revenue_kind = "one_time",
                        gross_amount_cents = amountDelta,
                        currency = txn.Currency,
                        provider = "stripe",
                        stripe_payment_intent_id = paymentIntentId,
                        scope = txn.Metadata?.Scope,
                        plan_id = txn.Metadata?.PlanId,
                        transaction_type = txn.TransactionType
                    },
                    "Refunded" => (object)new
                    {
                        revenue_kind = "one_time",
                        refund_amount_cents = Math.Abs(amountDelta),
                        currency = txn.Currency,
                        provider = "stripe",
                        stripe_payment_intent_id = paymentIntentId,
                        transaction_type = txn.TransactionType
                    },
                    _ => (object)new { }
                };

                var envelope = new EconomicEventEnvelope
                {
                    EventId = $"stripe:{providerEventId}",
                    EventType = economicType,
                    OccurredAt = DateTimeOffset.UtcNow,
                    Source = new EconomicEventSource
                    {
                        Producer = "control_plane",
                        Service = "payment",
                        AppId = appId,
                        Environment = string.Empty,
                        RequestId = _correlation.CorrelationId
                    },
                    Actor = new EconomicEventActor
                    {
                        ActorType = string.IsNullOrWhiteSpace(userId) ? "system" : "user",
                        ActorId = userId
                    },
                    Correlation = new EconomicEventCorrelation
                    {
                        UserId = userId,
                        TransactionId = txn.Id
                    },
                    Payload = JsonSerializer.SerializeToElement(payload)
                };

                await _economicEvents.TryAppendAsync(envelope);
            }
            catch (Exception ex)
            {
                var actor = new ActorContext { UserId = userId, AppId = txn.AppId };
                var entity = new EntityContext { PaymentIntentId = paymentIntentId, AppId = txn.AppId };
                _logs.Warn("EconomicProtocol.EmitFailed", actor, entity, new { eventType = economicType, error = ex.Message });
            }
        }

        private static bool IsMozaiksPayOneTimeRevenue(TransactionModel txn)
            => string.Equals(txn.TransactionType, "AppOneTimePayment", StringComparison.OrdinalIgnoreCase)
               || string.Equals(txn.TransactionType, "PlatformOneTimePayment", StringComparison.OrdinalIgnoreCase);
    }

    public class PaymentStatusResult
    {
        public string PaymentIntentId { get; set; } = string.Empty;
        public string ProviderStatus { get; set; } = "unknown";
        public string? LocalStatus { get; set; }
        public string? WalletId { get; set; }
        public long? Amount { get; set; }
        public string? Currency { get; set; }
    }

    public class PaymentIntentResult
    {
        public bool Success { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? ClientSecret { get; set; }
        public string? ErrorReason { get; set; }
    }

    public class PaymentConfirmResult
    {
        public bool Success { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? Status { get; set; }
    }

    public class RefundResult
    {
        public bool Success { get; set; }
        public string? RefundId { get; set; }
        public string? Status { get; set; }
    }
}
