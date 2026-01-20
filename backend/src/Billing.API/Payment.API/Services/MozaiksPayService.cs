using System.Diagnostics;
using System.Text.Json;
using EventBus.Messages.EconomicProtocol;
using Payment.API.DTOs;
using Payment.API.Infrastructure.Observability;
using Payment.API.Models;
using Stripe;

namespace Payment.API.Services
{
    public class MozaiksPayService
    {
        private readonly PaymentService _payments;
        private readonly TransactionService _transactions;
        private readonly SubscriptionPlanClient _subscriptionPlans;
        private readonly ObservabilityTracing _tracing;
        private readonly ObservabilityMetrics _metrics;
        private readonly StructuredLogEmitter _logs;
        private readonly AnalyticsEventEmitter _analytics;
        private readonly ICorrelationContextAccessor _correlation;
        private readonly EconomicEventAppender _economicEvents;

        private readonly CustomerService _customerService;
        private readonly SubscriptionService _subscriptionService;
        private readonly InvoiceService _invoiceService;
        private readonly PaymentIntentService _paymentIntentService;
        private readonly PriceService _priceService;

        public MozaiksPayService(
            PaymentService payments,
            TransactionService transactions,
            SubscriptionPlanClient subscriptionPlans,
            ObservabilityTracing tracing,
            ObservabilityMetrics metrics,
            StructuredLogEmitter logs,
            AnalyticsEventEmitter analytics,
            ICorrelationContextAccessor correlation,
            EconomicEventAppender economicEvents,
            IConfiguration configuration)
        {
            _payments = payments;
            _transactions = transactions;
            _subscriptionPlans = subscriptionPlans;
            _tracing = tracing;
            _metrics = metrics;
            _logs = logs;
            _analytics = analytics;
            _correlation = correlation;
            _economicEvents = economicEvents;

            var secretKey = configuration.GetValue<string>("Payments:SecretKey");
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException("Payments secret key is not configured.");
            }
            StripeConfiguration.ApiKey = secretKey;

            _customerService = new CustomerService();
            _subscriptionService = new SubscriptionService();
            _invoiceService = new InvoiceService();
            _paymentIntentService = new PaymentIntentService();
            _priceService = new PriceService();
        }

        public async Task<MozaiksPayCheckoutResponse> CreateCheckoutAsync(string userId, MozaiksPayCheckoutRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new InvalidOperationException("UserId is required.");
            }

            var scope = NormalizeScope(request.Scope);
            var mode = NormalizeMode(request.Mode);

            if (scope == MozaiksPayScopes.App && string.IsNullOrWhiteSpace(request.AppId))
            {
                throw new ArgumentException("AppId is required for app scope.");
            }

            if (mode == MozaiksPayModes.Payment)
            {
                if (!request.Amount.HasValue || request.Amount.Value <= 0)
                {
                    throw new ArgumentException("Amount is required for one-time payments.");
                }

                return await CreateOneTimePaymentCheckoutAsync(userId, request, cancellationToken);
            }

            return await CreateSubscriptionCheckoutAsync(userId, request, cancellationToken);
        }

        public async Task<MozaiksPayStatusResponse> GetSubscriptionStatusAsync(string userId, string scope, string? appId, CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            var normalizedScope = NormalizeScope(scope);
            if (normalizedScope == MozaiksPayScopes.App && string.IsNullOrWhiteSpace(appId))
            {
                throw new ArgumentException("AppId is required for app scope.");
            }

            var contractType = GetContractTransactionType(normalizedScope);
            var contract = await _transactions.GetLatestByTypeAsync(contractType, userId, normalizedScope == MozaiksPayScopes.App ? appId : null);

            if (contract == null)
            {
                return new MozaiksPayStatusResponse { IsActive = false };
            }

            var expiresAt = contract.Metadata?.CurrentPeriodEndUtc;
            var isActive = string.Equals(contract.Status, "Active", StringComparison.OrdinalIgnoreCase)
                && (!expiresAt.HasValue || expiresAt.Value > DateTime.UtcNow);

            return new MozaiksPayStatusResponse
            {
                IsActive = isActive,
                PlanId = contract.Metadata?.PlanId,
                ExpiresAtUtc = expiresAt,
                SubscriptionId = contract.Metadata?.SubscriptionId,
                CurrentPeriodEndUtc = contract.Metadata?.CurrentPeriodEndUtc,
                Features = new List<string>()
            };
        }

        public async Task<MonetizationPriceProvisionResponse> ProvisionMonetizationPriceAsync(MonetizationPriceProvisionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.AppId))
            {
                throw new ArgumentException("AppId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PlanId))
            {
                throw new ArgumentException("PlanId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PlanName))
            {
                throw new ArgumentException("PlanName is required.");
            }

            if (request.AmountCents <= 0)
            {
                throw new ArgumentException("AmountCents must be > 0.");
            }

            if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
            {
                throw new ArgumentException("Currency must be a 3-letter code.");
            }

            if (string.IsNullOrWhiteSpace(request.BillingInterval))
            {
                throw new ArgumentException("BillingInterval is required.");
            }

            if (string.IsNullOrWhiteSpace(request.SpecHash))
            {
                throw new ArgumentException("SpecHash is required.");
            }

            var lookupKey = BuildMonetizationLookupKey(request);

            var existing = await _priceService.ListAsync(
                new PriceListOptions
                {
                    Active = true,
                    LookupKeys = new List<string> { lookupKey },
                    Limit = 1
                },
                requestOptions: null,
                cancellationToken: cancellationToken);

            var price = existing?.Data?.FirstOrDefault();
            if (price != null && !string.IsNullOrWhiteSpace(price.Id))
            {
                return new MonetizationPriceProvisionResponse
                {
                    Succeeded = true,
                    StripePriceId = price.Id,
                    StripeProductId = price.ProductId,
                    StripeLookupKey = lookupKey
                };
            }

            var options = new PriceCreateOptions
            {
                LookupKey = lookupKey,
                Currency = request.Currency.Trim().ToLowerInvariant(),
                UnitAmount = request.AmountCents,
                Recurring = new PriceRecurringOptions { Interval = request.BillingInterval.Trim().ToLowerInvariant() },
                ProductData = new PriceProductDataOptions
                {
                    Name = request.PlanName.Trim(),
                    Metadata = new Dictionary<string, string>
                    {
                        { "appId", request.AppId },
                        { "planId", request.PlanId },
                        { "specVersion", request.SpecVersion.ToString() },
                        { "specHash", request.SpecHash },
                        { "proposalId", request.ProposalId ?? string.Empty }
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    { "appId", request.AppId },
                    { "planId", request.PlanId },
                    { "specVersion", request.SpecVersion.ToString() },
                    { "specHash", request.SpecHash },
                    { "proposalId", request.ProposalId ?? string.Empty }
                }
            };

            var created = await _priceService.CreateAsync(
                options,
                requestOptions: new RequestOptions { IdempotencyKey = lookupKey },
                cancellationToken: cancellationToken);

            if (created == null || string.IsNullOrWhiteSpace(created.Id))
            {
                return new MonetizationPriceProvisionResponse
                {
                    Succeeded = false,
                    Error = "StripePriceCreateFailed"
                };
            }

            return new MonetizationPriceProvisionResponse
            {
                Succeeded = true,
                StripePriceId = created.Id,
                StripeProductId = created.ProductId,
                StripeLookupKey = lookupKey
            };
        }

        public async Task CancelAsync(string userId, MozaiksPayCancelRequest request, CancellationToken cancellationToken)
        {
            var scope = NormalizeScope(request.Scope);
            if (scope == MozaiksPayScopes.App && string.IsNullOrWhiteSpace(request.AppId))
            {
                throw new ArgumentException("AppId is required for app scope.");
            }

            var contractType = GetContractTransactionType(scope);
            var contract = await _transactions.GetLatestByTypeAsync(contractType, userId, scope == MozaiksPayScopes.App ? request.AppId : null);
            if (contract == null || string.IsNullOrWhiteSpace(contract.Metadata?.SubscriptionId))
            {
                return;
            }

            var subscriptionId = contract.Metadata.SubscriptionId;

            using var span = _tracing.StartSpan("mozaikspay.subscription_cancel", new ObservabilitySpanContext
            {
                CorrelationId = _correlation.CorrelationId,
                UserId = userId,
                AppId = request.AppId
            });

            try
            {
                var options = new SubscriptionCancelOptions { InvoiceNow = false, Prorate = false };
                await _subscriptionService.CancelAsync(subscriptionId, options, requestOptions: null, cancellationToken);

                await _transactions.UpdateSubscriptionContractAsync(contract.Id ?? string.Empty, "Cancelled", contract.Metadata.CurrentPeriodEndUtc);
                _logs.Info("MozaiksPay.Subscription.Cancelled", new ActorContext { UserId = userId, AppId = request.AppId }, new EntityContext { AppId = request.AppId }, new { _correlation.CorrelationId });
                _analytics.Emit("server.mozaikspay.subscription_cancelled", new ActorContext { UserId = userId, AppId = request.AppId }, new EntityContext { AppId = request.AppId }, new { });
            }
            catch (StripeException ex)
            {
                _logs.Error("MozaiksPay.Subscription.CancelFailed", new ActorContext { UserId = userId, AppId = request.AppId }, new EntityContext { AppId = request.AppId }, new { reason = ex.StripeError?.Code ?? "ProviderError" });
                throw;
            }
        }

        public async Task HandleWebhookEventAsync(Event providerEvent, CancellationToken cancellationToken = default)
        {
            if (providerEvent == null) return;

            switch (providerEvent.Type)
            {
                case "invoice.payment_succeeded":
                    if (providerEvent.Data.Object is Invoice invoice)
                    {
                        await HandleInvoicePaidAsync(invoice, cancellationToken);
                    }
                    break;
                case "invoice.payment_failed":
                    if (providerEvent.Data.Object is Invoice failedInvoice)
                    {
                        await HandleInvoiceFailedAsync(failedInvoice, cancellationToken);
                    }
                    break;
                case "customer.subscription.updated":
                    if (providerEvent.Data.Object is Subscription subscription)
                    {
                        await HandleSubscriptionUpdatedAsync(subscription, cancellationToken);
                    }
                    break;
                case "customer.subscription.deleted":
                    if (providerEvent.Data.Object is Subscription deleted)
                    {
                        await HandleSubscriptionDeletedAsync(deleted, cancellationToken);
                    }
                    break;
            }
        }

        private async Task<MozaiksPayCheckoutResponse> CreateOneTimePaymentCheckoutAsync(string userId, MozaiksPayCheckoutRequest request, CancellationToken cancellationToken)
        {
            using var span = _tracing.StartSpan("mozaikspay.checkout_payment", new ObservabilitySpanContext
            {
                CorrelationId = _correlation.CorrelationId,
                UserId = userId,
                AppId = request.AppId
            });

            var actor = new ActorContext { UserId = userId, AppId = request.AppId };
            var entity = new EntityContext { AppId = request.AppId };

            var pi = await _payments.CreatePaymentIntentAsync(new PaymentIntentRequest
            {
                Amount = request.Amount ?? 0,
                Currency = request.Currency ?? "usd",
                UserId = userId,
                WalletId = string.Empty,
                AppId = request.AppId ?? string.Empty,
                TransactionType = scopeToTransactionType(NormalizeScope(request.Scope), MozaiksPayModes.Payment),
                SubscriptionId = request.PlanId
            });

            if (!pi.Success || string.IsNullOrWhiteSpace(pi.PaymentIntentId) || string.IsNullOrWhiteSpace(pi.ClientSecret))
            {
                _metrics.RecordPaymentConfirmFailed("MozaiksPayCreatePaymentFailed");
                _logs.Error("MozaiksPay.Checkout.PaymentFailed", actor, entity, new { _correlation.CorrelationId, request.PlanId });
                throw new InvalidOperationException(pi.ErrorReason ?? "PaymentFailed");
            }

            var localTxn = await _transactions.GetByIntentIdAsync(pi.PaymentIntentId);
            var sessionId = localTxn?.Id ?? string.Empty;

            _logs.Info("MozaiksPay.Checkout.PaymentCreated", actor, entity, new { _correlation.CorrelationId, sessionId, request.PlanId });
            _analytics.Emit("server.mozaikspay.checkout_created", actor, entity, new { request.Mode, request.Scope, request.PlanId });

            return new MozaiksPayCheckoutResponse
            {
                SessionId = sessionId,
                ClientSecret = pi.ClientSecret,
                Mode = MozaiksPayModes.Payment,
                Status = "pending"
            };

            static string scopeToTransactionType(string scope, string mode)
            {
                if (mode == MozaiksPayModes.Payment)
                {
                    return scope == MozaiksPayScopes.App ? "AppOneTimePayment" : "PlatformOneTimePayment";
                }
                return scope == MozaiksPayScopes.App ? "AppSubscriptionPayment" : "PlatformSubscriptionPayment";
            }
        }

        private async Task<MozaiksPayCheckoutResponse> CreateSubscriptionCheckoutAsync(string userId, MozaiksPayCheckoutRequest request, CancellationToken cancellationToken)
        {
            var scope = NormalizeScope(request.Scope);

            var amount = request.Amount;
            var planName = request.PlanId;
            if (scope == MozaiksPayScopes.Platform && (!amount.HasValue || amount.Value <= 0))
            {
                var plan = await _subscriptionPlans.GetPlanAsync(request.PlanId, cancellationToken);
                if (plan == null || plan.Price <= 0)
                {
                    throw new ArgumentException("Invalid planId for platform subscription.");
                }

                amount = (long)Math.Round(plan.Price * 100m, MidpointRounding.AwayFromZero);
                if (!string.IsNullOrWhiteSpace(plan.Name))
                {
                    planName = plan.Name;
                }
            }

            if (!amount.HasValue || amount.Value <= 0)
            {
                throw new ArgumentException("Amount is required for subscription checkout.");
            }

            using var span = _tracing.StartSpan("mozaikspay.checkout_subscription", new ObservabilitySpanContext
            {
                CorrelationId = _correlation.CorrelationId,
                UserId = userId,
                AppId = request.AppId
            });

            var actor = new ActorContext { UserId = userId, AppId = request.AppId };
            var entity = new EntityContext { AppId = request.AppId };

            var contractType = GetContractTransactionType(scope);

            var existing = await _transactions.GetLatestByTypeAsync(contractType, userId, scope == MozaiksPayScopes.App ? request.AppId : null);
            if (existing != null && string.Equals(existing.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return new MozaiksPayCheckoutResponse
                {
                    SessionId = existing.Id ?? string.Empty,
                    ClientSecret = string.Empty,
                    Mode = MozaiksPayModes.Subscription,
                    Status = "already_active"
                };
            }

            var customer = await _customerService.CreateAsync(new CustomerCreateOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId },
                    { "scope", scope },
                    { "appId", request.AppId ?? string.Empty },
                    { "planId", request.PlanId },
                    { "correlationId", _correlation.CorrelationId }
                }
            }, requestOptions: null, cancellationToken);

            var priceId = await GetOrCreateMonthlyRecurringPriceIdAsync(
                scope,
                request.AppId,
                request.PlanId,
                planName ?? request.PlanId,
                amount.Value,
                request.Currency ?? "usd",
                cancellationToken);

            var options = new SubscriptionCreateOptions
            {
                Customer = customer.Id,
                PaymentBehavior = "default_incomplete",
                PaymentSettings = new SubscriptionPaymentSettingsOptions
                {
                    SaveDefaultPaymentMethod = "on_subscription"
                },
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Price = priceId
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId },
                    { "scope", scope },
                    { "appId", request.AppId ?? string.Empty },
                    { "planId", request.PlanId },
                    { "correlationId", _correlation.CorrelationId }
                },
                Expand = new List<string> { "latest_invoice" }
            };

            var subscription = await _subscriptionService.CreateAsync(options, requestOptions: null, cancellationToken);

            var invoice = subscription.LatestInvoice;
            if (invoice == null && !string.IsNullOrWhiteSpace(subscription.LatestInvoiceId))
            {
                invoice = await _invoiceService.GetAsync(
                    subscription.LatestInvoiceId,
                    new InvoiceGetOptions { Expand = new List<string> { "payments", "lines" } },
                    requestOptions: null,
                    cancellationToken: cancellationToken);
            }

            var clientSecret = invoice?.ConfirmationSecret?.ClientSecret ?? string.Empty;

            var paymentIntent = TryGetPaymentIntent(invoice);
            var paymentIntentId = TryGetPaymentIntentId(invoice);
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                clientSecret = paymentIntent?.ClientSecret ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(paymentIntentId))
            {
                paymentIntent = await _paymentIntentService.GetAsync(paymentIntentId, requestOptions: null, cancellationToken: cancellationToken);
                clientSecret = paymentIntent?.ClientSecret ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                _logs.Error("MozaiksPay.Checkout.SubscriptionMissingClientSecret", actor, entity, new { _correlation.CorrelationId, subscriptionId = subscription.Id });
                throw new InvalidOperationException("SubscriptionPaymentSetupFailed");
            }

            var currentPeriodEndUtc = NormalizeUtc(invoice?.PeriodEnd);

            var contractTxn = new TransactionModel
            {
                TransactionType = contractType,
                Amount = amount.Value,
                Currency = request.Currency ?? "usd",
                WalletId = string.Empty,
                AppId = request.AppId ?? string.Empty,
                PaymentIntentId = string.Empty,
                Status = "Incomplete",
                Metadata = new TransactionMetadata
                {
                    PayerUserId = userId,
                    AppCreatorId = string.Empty,
                    SubscriptionId = subscription.Id ?? string.Empty,
                    Scope = scope,
                    PlanId = request.PlanId,
                    CustomerRef = customer.Id,
                    CurrentPeriodEndUtc = currentPeriodEndUtc
                }
            };

            await _transactions.CreateTransactionAsync(contractTxn);

            var paymentTxn = new TransactionModel
            {
                TransactionType = scope == MozaiksPayScopes.App ? "AppSubscriptionPayment" : "PlatformSubscriptionPayment",
                Amount = paymentIntent?.Amount ?? amount.Value,
                Currency = paymentIntent?.Currency ?? (request.Currency ?? "usd"),
                WalletId = string.Empty,
                AppId = request.AppId ?? string.Empty,
                PaymentIntentId = paymentIntent?.Id ?? paymentIntentId ?? string.Empty,
                Status = "Pending",
                Metadata = new TransactionMetadata
                {
                    PayerUserId = userId,
                    AppCreatorId = string.Empty,
                    SubscriptionId = subscription.Id ?? string.Empty,
                    Scope = scope,
                    PlanId = request.PlanId,
                    CustomerRef = customer.Id,
                    CurrentPeriodEndUtc = currentPeriodEndUtc
                }
            };

            await _transactions.CreateTransactionAsync(paymentTxn);

            _logs.Info("MozaiksPay.Checkout.SubscriptionCreated", actor, entity, new { _correlation.CorrelationId, contractId = contractTxn.Id, subscriptionId = subscription.Id });
            _analytics.Emit("server.mozaikspay.checkout_created", actor, entity, new { request.Mode, request.Scope, request.PlanId });

            return new MozaiksPayCheckoutResponse
            {
                SessionId = contractTxn.Id ?? string.Empty,
                ClientSecret = clientSecret,
                Mode = MozaiksPayModes.Subscription,
                Status = "pending"
            };
        }

        private async Task HandleInvoicePaidAsync(Invoice invoice, CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            var subscriptionId = TryGetSubscriptionId(invoice);
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return;
            }

            var contract = await _transactions.GetBySubscriptionIdAsync(subscriptionId);
            if (contract == null || string.IsNullOrWhiteSpace(contract.Id))
            {
                return;
            }

            var currentPeriodEndUtc = NormalizeUtc(invoice.PeriodEnd) ?? contract.Metadata?.CurrentPeriodEndUtc;

            await _transactions.UpdateSubscriptionContractAsync(contract.Id, "Active", currentPeriodEndUtc);

            var actor = new ActorContext { UserId = contract.Metadata?.PayerUserId, AppId = contract.AppId };
            var entity = new EntityContext { AppId = contract.AppId };
            _logs.Info("MozaiksPay.Subscription.InvoicePaid", actor, entity, new { subscriptionId });
            _analytics.Emit("server.mozaikspay.subscription_invoice_paid", actor, entity, new { });

            await TryAppendRevenueInvoicePaidAsync(invoice, contract, subscriptionId, cancellationToken);
        }

        private async Task TryAppendRevenueInvoicePaidAsync(
            Invoice invoice,
            TransactionModel contract,
            string subscriptionId,
            CancellationToken cancellationToken)
        {
            try
            {
                var appId = string.IsNullOrWhiteSpace(contract.AppId) ? "platform" : contract.AppId;
                var userId = contract.Metadata?.PayerUserId;

                var envelope = new EconomicEventEnvelope
                {
                    EventId = string.IsNullOrWhiteSpace(invoice.Id) ? $"stripe:invoice_paid:{subscriptionId}" : $"stripe:{invoice.Id}",
                    EventType = EconomicEventTypes.RevenueInvoicePaid,
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
                        TransactionId = contract.Id
                    },
                    Payload = JsonSerializer.SerializeToElement(new
                    {
                        revenue_kind = "subscription",
                        gross_amount_cents = invoice.AmountPaid,
                        currency = invoice.Currency,
                        provider = "stripe",
                        stripe_invoice_id = invoice.Id,
                        stripe_subscription_id = subscriptionId,
                        stripe_payment_intent_id = TryGetPaymentIntentId(invoice),
                        scope = contract.Metadata?.Scope,
                        plan_id = contract.Metadata?.PlanId
                    })
                };

                await _economicEvents.TryAppendAsync(envelope, cancellationToken);
            }
            catch (Exception ex)
            {
                var actor = new ActorContext { UserId = contract.Metadata?.PayerUserId, AppId = contract.AppId };
                var entity = new EntityContext { AppId = contract.AppId };
                _logs.Warn("EconomicProtocol.EmitFailed", actor, entity, new { eventType = EconomicEventTypes.RevenueInvoicePaid, error = ex.Message });
            }
        }

        private async Task HandleInvoiceFailedAsync(Invoice invoice, CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            var subscriptionId = TryGetSubscriptionId(invoice);
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return;
            }

            var contract = await _transactions.GetBySubscriptionIdAsync(subscriptionId);
            if (contract == null || string.IsNullOrWhiteSpace(contract.Id))
            {
                return;
            }

            await _transactions.UpdateSubscriptionContractAsync(contract.Id, "PastDue", contract.Metadata?.CurrentPeriodEndUtc);

            var actor = new ActorContext { UserId = contract.Metadata?.PayerUserId, AppId = contract.AppId };
            var entity = new EntityContext { AppId = contract.AppId };
            _logs.Warn("MozaiksPay.Subscription.InvoiceFailed", actor, entity, new { subscriptionId });
            _analytics.Emit("server.mozaikspay.subscription_invoice_failed", actor, entity, new { });
        }

        private async Task HandleSubscriptionUpdatedAsync(Subscription subscription, CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            var subscriptionId = subscription.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return;
            }

            var contract = await _transactions.GetBySubscriptionIdAsync(subscriptionId);
            if (contract == null || string.IsNullOrWhiteSpace(contract.Id))
            {
                return;
            }

            var currentPeriodEndUtc = contract.Metadata?.CurrentPeriodEndUtc;

            var status = subscription.Status switch
            {
                "active" => "Active",
                "trialing" => "Active",
                "past_due" => "PastDue",
                "canceled" => "Cancelled",
                "unpaid" => "PastDue",
                _ => contract.Status
            };

            await _transactions.UpdateSubscriptionContractAsync(contract.Id, status, currentPeriodEndUtc);
        }

        private async Task HandleSubscriptionDeletedAsync(Subscription subscription, CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            var subscriptionId = subscription.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return;
            }

            var contract = await _transactions.GetBySubscriptionIdAsync(subscriptionId);
            if (contract == null || string.IsNullOrWhiteSpace(contract.Id))
            {
                return;
            }

            await _transactions.UpdateSubscriptionContractAsync(contract.Id, "Cancelled", contract.Metadata?.CurrentPeriodEndUtc);
        }

        private static string NormalizeScope(string? scope)
        {
            var v = (scope ?? string.Empty).Trim().ToLowerInvariant();
            return v switch
            {
                MozaiksPayScopes.Platform => MozaiksPayScopes.Platform,
                MozaiksPayScopes.App => MozaiksPayScopes.App,
                _ => MozaiksPayScopes.Platform
            };
        }

        private static string NormalizeMode(string? mode)
        {
            var v = (mode ?? string.Empty).Trim().ToLowerInvariant();
            return v switch
            {
                MozaiksPayModes.Subscription => MozaiksPayModes.Subscription,
                MozaiksPayModes.Payment => MozaiksPayModes.Payment,
                _ => MozaiksPayModes.Subscription
            };
        }

        private static string GetContractTransactionType(string scope)
        {
            return scope == MozaiksPayScopes.App ? "AppSubscriptionContract" : "PlatformSubscriptionContract";
        }

        private static string? TryGetSubscriptionId(Invoice invoice)
        {
            return invoice.Lines?.Data?
                .Select(line => line.SubscriptionId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        private static string? TryGetPaymentIntentId(Invoice? invoice)
        {
            return invoice?.Payments?.Data?
                .Select(p => p.Payment?.PaymentIntentId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        private static PaymentIntent? TryGetPaymentIntent(Invoice? invoice)
        {
            return invoice?.Payments?.Data?
                .Select(p => p.Payment?.PaymentIntent)
                .FirstOrDefault(pi => pi != null);
        }

        private static DateTime? NormalizeUtc(DateTime? value)
        {
            if (!value.HasValue || value.Value == default)
            {
                return null;
            }

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }

        private async Task<string> GetOrCreateMonthlyRecurringPriceIdAsync(
            string scope,
            string? appId,
            string planId,
            string productName,
            long unitAmount,
            string currency,
            CancellationToken cancellationToken)
        {
            var lookupKey = BuildLookupKey(scope, appId, planId, unitAmount, currency);

            var existing = await _priceService.ListAsync(
                new PriceListOptions
                {
                    Active = true,
                    LookupKeys = new List<string> { lookupKey },
                    Limit = 1
                },
                requestOptions: null,
                cancellationToken: cancellationToken);

            var price = existing?.Data?.FirstOrDefault();
            if (price != null && !string.IsNullOrWhiteSpace(price.Id))
            {
                return price.Id;
            }

            var created = await _priceService.CreateAsync(
                new PriceCreateOptions
                {
                    LookupKey = lookupKey,
                    Currency = currency,
                    UnitAmount = unitAmount,
                    Recurring = new PriceRecurringOptions { Interval = "month" },
                    ProductData = new PriceProductDataOptions
                    {
                        Name = productName,
                        Metadata = new Dictionary<string, string>
                        {
                            { "scope", scope },
                            { "appId", appId ?? string.Empty },
                            { "planId", planId }
                        }
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "scope", scope },
                        { "appId", appId ?? string.Empty },
                        { "planId", planId }
                    }
                },
                requestOptions: null,
                cancellationToken: cancellationToken);

            if (created == null || string.IsNullOrWhiteSpace(created.Id))
            {
                throw new InvalidOperationException("SubscriptionPriceCreateFailed");
            }

            return created.Id;
        }

        private static string BuildLookupKey(string scope, string? appId, string planId, long unitAmount, string currency)
        {
            return string.Join(
                "_",
                "mozaikspay",
                NormalizeLookupKeyPart(scope),
                NormalizeLookupKeyPart(appId ?? "none"),
                NormalizeLookupKeyPart(planId),
                NormalizeLookupKeyPart(currency),
                unitAmount.ToString(),
                "monthly");
        }

        private static string BuildMonetizationLookupKey(MonetizationPriceProvisionRequest request)
        {
            return string.Join(
                "_",
                "mozaiks",
                "monetization",
                NormalizeLookupKeyPart(request.AppId),
                NormalizeLookupKeyPart(request.PlanId),
                NormalizeLookupKeyPart(request.SpecVersion.ToString()),
                NormalizeLookupKeyPart(request.Currency),
                request.AmountCents.ToString(),
                NormalizeLookupKeyPart(request.BillingInterval));
        }

        private static string NormalizeLookupKeyPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "none";
            }

            var normalized = new string(value
                .Trim()
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray());

            while (normalized.Contains("__", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
            }

            return normalized.Trim('_');
        }
    }
}
