// mozaiks-core: Payment Provider Contract
// This is the boundary between Core and any payment implementation.
// Platform implements with Stripe. Self-hosters can BYO.

namespace MozaiksBilling.Contracts;

/// <summary>
/// Abstract payment provider interface.
/// 
/// Implementations:
/// - PlatformPaymentProvider: Delegates to mozaiks-platform Payment.API
/// - NoOpPaymentProvider: Development/free tier (always returns active)
/// - Custom: Self-hosters implement for their own payment processor
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Provider identifier for logging and debugging.
    /// Examples: "platform", "noop", "stripe-direct", "paddle"
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Create a checkout session for subscription or one-time payment.
    /// </summary>
    /// <param name="request">Checkout details including plan, user, amounts</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Checkout session with URL or client secret for Stripe Elements</returns>
    Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get current subscription status for a user/app.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="scope">"platform" for platform subscription, "app" for app-specific</param>
    /// <param name="appId">App identifier (required when scope is "app")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Current subscription status including features and limits</returns>
    Task<SubscriptionStatus> GetSubscriptionStatusAsync(
        string userId, 
        string scope, 
        string? appId = null, 
        CancellationToken ct = default);

    /// <summary>
    /// Cancel an active subscription.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="scope">"platform" for platform subscription, "app" for app-specific</param>
    /// <param name="appId">App identifier (required when scope is "app")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cancellation result</returns>
    Task<CancelResult> CancelSubscriptionAsync(
        string userId, 
        string scope, 
        string? appId = null, 
        CancellationToken ct = default);

    /// <summary>
    /// Process a webhook from the payment provider.
    /// Validates signature and returns events to be handled.
    /// </summary>
    /// <param name="payload">Raw webhook payload</param>
    /// <param name="signature">Signature header for validation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Parsed webhook events</returns>
    Task<WebhookResult> ProcessWebhookAsync(
        string payload, 
        string signature, 
        CancellationToken ct = default);
}

#region Request/Response Models

/// <summary>
/// Checkout session request
/// </summary>
public record CheckoutRequest
{
    /// <summary>User initiating the checkout</summary>
    public required string UserId { get; init; }
    
    /// <summary>"platform" or "app"</summary>
    public required string Scope { get; init; }
    
    /// <summary>App ID (required when scope is "app")</summary>
    public string? AppId { get; init; }
    
    /// <summary>Plan to subscribe to (e.g., "starter_monthly", "pro_annual")</summary>
    public required string PlanId { get; init; }
    
    /// <summary>"subscription" for recurring, "payment" for one-time</summary>
    public required string Mode { get; init; }
    
    /// <summary>Amount in cents (for one-time payments)</summary>
    public long? AmountCents { get; init; }
    
    /// <summary>Currency code (e.g., "usd", "eur")</summary>
    public string Currency { get; init; } = "usd";
    
    /// <summary>URL to redirect after successful payment</summary>
    public string? SuccessUrl { get; init; }
    
    /// <summary>URL to redirect if user cancels</summary>
    public string? CancelUrl { get; init; }
    
    /// <summary>Additional metadata to attach to the payment</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Checkout session result
/// </summary>
public record CheckoutResult
{
    /// <summary>Checkout session ID (provider-specific)</summary>
    public required string SessionId { get; init; }
    
    /// <summary>Client secret for Stripe Elements, or checkout URL</summary>
    public required string ClientSecret { get; init; }
    
    /// <summary>Session status: "created", "pending", "complete", "expired"</summary>
    public required string Status { get; init; }
    
    /// <summary>Full checkout URL if using hosted checkout</summary>
    public string? CheckoutUrl { get; init; }
    
    /// <summary>Expiration time for the session</summary>
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Subscription status response
/// </summary>
public record SubscriptionStatus
{
    /// <summary>Whether subscription is currently active</summary>
    public required bool IsActive { get; init; }
    
    /// <summary>Current plan ID</summary>
    public string? PlanId { get; init; }
    
    /// <summary>Human-readable plan name</summary>
    public string? PlanName { get; init; }
    
    /// <summary>Plan tier: free, starter, pro, enterprise, unlimited</summary>
    public string? Tier { get; init; }
    
    /// <summary>When subscription expires (null = never/cancelled)</summary>
    public DateTime? ExpiresAtUtc { get; init; }
    
    /// <summary>Provider's subscription ID</summary>
    public string? SubscriptionId { get; init; }
    
    /// <summary>List of enabled feature flags</summary>
    public List<string> Features { get; init; } = new();
    
    /// <summary>Token budget information</summary>
    public TokenBudgetInfo? TokenBudget { get; init; }
    
    /// <summary>Current period start</summary>
    public DateTime? CurrentPeriodStart { get; init; }
    
    /// <summary>Current period end</summary>
    public DateTime? CurrentPeriodEnd { get; init; }
    
    /// <summary>Whether subscription will cancel at period end</summary>
    public bool CancelAtPeriodEnd { get; init; }
}

/// <summary>
/// Token budget information within subscription status
/// </summary>
public record TokenBudgetInfo
{
    /// <summary>Budget period: monthly, unlimited</summary>
    public required string Period { get; init; }
    
    /// <summary>Token limit (-1 = unlimited)</summary>
    public required long Limit { get; init; }
    
    /// <summary>Tokens used this period</summary>
    public required long Used { get; init; }
    
    /// <summary>Enforcement level: none, warn, soft, hard</summary>
    public required string Enforcement { get; init; }
}

/// <summary>
/// Cancellation result
/// </summary>
public record CancelResult
{
    /// <summary>Whether cancellation succeeded</summary>
    public required bool Success { get; init; }
    
    /// <summary>Human-readable message</summary>
    public string? Message { get; init; }
    
    /// <summary>When subscription will actually end (if canceling at period end)</summary>
    public DateTime? EffectiveAt { get; init; }
    
    /// <summary>Whether immediately cancelled or at period end</summary>
    public bool ImmediatelyCancelled { get; init; }
}

/// <summary>
/// Webhook processing result
/// </summary>
public record WebhookResult
{
    /// <summary>Whether webhook was successfully processed</summary>
    public required bool Processed { get; init; }
    
    /// <summary>List of events extracted from webhook</summary>
    public List<WebhookEvent> Events { get; init; } = new();
    
    /// <summary>Error message if processing failed</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Individual webhook event
/// </summary>
public record WebhookEvent
{
    /// <summary>
    /// Event type. Standard types:
    /// - subscription.created
    /// - subscription.updated
    /// - subscription.cancelled
    /// - subscription.renewed
    /// - payment.succeeded
    /// - payment.failed
    /// - invoice.paid
    /// - invoice.payment_failed
    /// </summary>
    public required string Type { get; init; }
    
    /// <summary>User ID associated with event</summary>
    public required string UserId { get; init; }
    
    /// <summary>App ID (if app-scoped event)</summary>
    public string? AppId { get; init; }
    
    /// <summary>Provider's subscription ID</summary>
    public string? SubscriptionId { get; init; }
    
    /// <summary>Event timestamp</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>Additional event data</summary>
    public Dictionary<string, object> Data { get; init; } = new();
}

#endregion
