// mozaiks-core: Platform Payment Provider
// Delegates all payment operations to mozaiks-platform Payment.API
// Used when running in platform-hosted mode.

using System.Net.Http.Json;
using System.Text.Json;
using MozaiksBilling.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MozaiksBilling.Providers;

/// <summary>
/// Payment provider that delegates to mozaiks-platform Payment.API.
/// 
/// This is the default provider when running in platform-hosted mode.
/// All Stripe operations are handled by the platform - Core never touches
/// Stripe keys directly.
/// 
/// Configuration:
/// - PlatformPaymentApiUrl: Base URL of platform Payment.API
/// - PlatformApiKey: API key for authentication
/// </summary>
public class PlatformPaymentProvider : IPaymentProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlatformPaymentProvider> _logger;
    private readonly PlatformPaymentOptions _options;

    public PlatformPaymentProvider(
        HttpClient httpClient,
        IOptions<PlatformPaymentOptions> options,
        ILogger<PlatformPaymentProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Configure base address and auth
        _httpClient.BaseAddress = new Uri(_options.PlatformPaymentApiUrl);
        if (!string.IsNullOrEmpty(_options.PlatformApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.PlatformApiKey);
        }
    }

    public string ProviderId => "platform";

    public async Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Platform] Creating checkout for user {UserId}, plan {PlanId}",
            request.UserId,
            request.PlanId
        );

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/payment/checkout",
                new
                {
                    request.UserId,
                    request.Scope,
                    request.AppId,
                    request.PlanId,
                    request.Mode,
                    request.AmountCents,
                    request.Currency,
                    request.SuccessUrl,
                    request.CancelUrl,
                    request.Metadata
                },
                ct
            );

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CheckoutResult>(ct);
            return result ?? throw new InvalidOperationException("Empty response from platform");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Platform] Failed to create checkout session");
            throw new PaymentProviderException("Failed to create checkout session", ex);
        }
    }

    public async Task<SubscriptionStatus> GetSubscriptionStatusAsync(
        string userId, 
        string scope, 
        string? appId = null, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "[Platform] Getting subscription status for user {UserId}, scope {Scope}, app {AppId}",
            userId,
            scope,
            appId
        );

        try
        {
            var url = $"/api/payment/subscription/{userId}/{scope}";
            if (!string.IsNullOrEmpty(appId))
            {
                url += $"?appId={Uri.EscapeDataString(appId)}";
            }

            var response = await _httpClient.GetAsync(url, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No subscription found - return inactive
                return new SubscriptionStatus
                {
                    IsActive = false,
                    PlanId = null,
                    Tier = "free",
                    Features = new List<string>()
                };
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SubscriptionStatus>(ct);
            return result ?? new SubscriptionStatus { IsActive = false };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Platform] Failed to get subscription status");
            
            // On platform error, fail open for self-hosted, fail closed for platform
            if (_options.FailOpenOnError)
            {
                _logger.LogWarning("[Platform] Failing open - returning unlimited access");
                return new SubscriptionStatus
                {
                    IsActive = true,
                    PlanId = "fallback",
                    Tier = "unlimited",
                    Features = new List<string> { "*" },
                    TokenBudget = new TokenBudgetInfo
                    {
                        Period = "unlimited",
                        Limit = -1,
                        Used = 0,
                        Enforcement = "none"
                    }
                };
            }
            
            throw new PaymentProviderException("Failed to get subscription status", ex);
        }
    }

    public async Task<CancelResult> CancelSubscriptionAsync(
        string userId, 
        string scope, 
        string? appId = null, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Platform] Cancelling subscription for user {UserId}, scope {Scope}",
            userId,
            scope
        );

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/payment/cancel",
                new
                {
                    UserId = userId,
                    Scope = scope,
                    AppId = appId
                },
                ct
            );

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CancelResult>(ct);
            return result ?? new CancelResult { Success = false, Message = "Empty response" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Platform] Failed to cancel subscription");
            throw new PaymentProviderException("Failed to cancel subscription", ex);
        }
    }

    public async Task<WebhookResult> ProcessWebhookAsync(
        string payload, 
        string signature, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("[Platform] Processing webhook");

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/payment/webhook/process",
                new
                {
                    Payload = payload,
                    Signature = signature
                },
                ct
            );

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<WebhookResult>(ct);
            return result ?? new WebhookResult { Processed = false, Error = "Empty response" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Platform] Failed to process webhook");
            return new WebhookResult
            {
                Processed = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Configuration options for PlatformPaymentProvider
/// </summary>
public class PlatformPaymentOptions
{
    public const string SectionName = "PlatformPayment";

    /// <summary>Base URL of mozaiks-platform Payment.API</summary>
    public string PlatformPaymentApiUrl { get; set; } = "http://payment-api:8080";

    /// <summary>API key for authenticating with platform</summary>
    public string PlatformApiKey { get; set; } = string.Empty;

    /// <summary>
    /// If true, return unlimited access when platform is unreachable.
    /// Set to false for strict platform-hosted mode.
    /// </summary>
    public bool FailOpenOnError { get; set; } = false;

    /// <summary>HTTP timeout in seconds</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Exception thrown when payment provider operations fail
/// </summary>
public class PaymentProviderException : Exception
{
    public PaymentProviderException(string message) : base(message) { }
    public PaymentProviderException(string message, Exception inner) : base(message, inner) { }
}
