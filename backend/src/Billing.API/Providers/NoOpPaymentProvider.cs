// mozaiks-core: NoOp Payment Provider
// Used for development, testing, and self-hosted free tier.
// Always returns "active" subscription - no actual payment processing.

using MozaiksBilling.Contracts;
using Microsoft.Extensions.Logging;

namespace MozaiksBilling.Providers;

/// <summary>
/// No-operation payment provider for development and self-hosted deployments.
/// 
/// Behavior:
/// - CreateCheckout: Returns a "complete" session immediately
/// - GetSubscriptionStatus: Always returns active with unlimited features
/// - CancelSubscription: Always succeeds
/// - ProcessWebhook: Returns empty (no events)
/// 
/// Use when:
/// - Local development without Stripe
/// - Self-hosted deployments without billing
/// - Testing entitlement/feature logic without payment integration
/// </summary>
public class NoOpPaymentProvider : IPaymentProvider
{
    private readonly ILogger<NoOpPaymentProvider> _logger;

    public NoOpPaymentProvider(ILogger<NoOpPaymentProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderId => "noop";

    public Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NoOp] CreateCheckout called for user {UserId}, plan {PlanId} - returning immediate success",
            request.UserId,
            request.PlanId
        );

        var sessionId = $"noop_session_{Guid.NewGuid():N}";

        return Task.FromResult(new CheckoutResult
        {
            SessionId = sessionId,
            ClientSecret = "noop_secret",
            Status = "complete",
            CheckoutUrl = null, // No actual checkout needed
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
    }

    public Task<SubscriptionStatus> GetSubscriptionStatusAsync(
        string userId, 
        string scope, 
        string? appId = null, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "[NoOp] GetSubscriptionStatus for user {UserId}, scope {Scope}, app {AppId} - returning unlimited",
            userId,
            scope,
            appId
        );

        return Task.FromResult(new SubscriptionStatus
        {
            IsActive = true,
            PlanId = "self-hosted",
            PlanName = "Self-Hosted (Unlimited)",
            Tier = "unlimited",
            ExpiresAtUtc = null, // Never expires
            SubscriptionId = null,
            Features = new List<string>
            {
                "workflow_execution",
                "multi_agent",
                "streaming",
                "function_calling",
                "code_execution",
                "file_uploads",
                "memory_persistence",
                "custom_models",
                "priority_queue",
                "dedicated_resources",
                "*" // Wildcard - all features
            },
            TokenBudget = new TokenBudgetInfo
            {
                Period = "unlimited",
                Limit = -1, // Unlimited
                Used = 0,
                Enforcement = "none"
            },
            CurrentPeriodStart = null,
            CurrentPeriodEnd = null,
            CancelAtPeriodEnd = false
        });
    }

    public Task<CancelResult> CancelSubscriptionAsync(
        string userId, 
        string scope, 
        string? appId = null, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NoOp] CancelSubscription called for user {UserId} - nothing to cancel",
            userId
        );

        return Task.FromResult(new CancelResult
        {
            Success = true,
            Message = "No subscription to cancel (NoOp provider)",
            EffectiveAt = null,
            ImmediatelyCancelled = true
        });
    }

    public Task<WebhookResult> ProcessWebhookAsync(
        string payload, 
        string signature, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("[NoOp] ProcessWebhook called - returning empty result");

        return Task.FromResult(new WebhookResult
        {
            Processed = true,
            Events = new List<WebhookEvent>(),
            Error = null
        });
    }
}
