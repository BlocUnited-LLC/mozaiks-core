using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AuthServer.Api.Services;

public interface IMonetizationStripeProvisioner
{
    Task<MonetizationStripePlanResult> ProvisionPlanAsync(MonetizationStripePlanRequest request, string? correlationId, CancellationToken cancellationToken);
}

public sealed class MonetizationStripeProvisioner : IMonetizationStripeProvisioner
{
    private readonly HttpClient _httpClient;
    private readonly IServiceToServiceTokenProvider _tokenProvider;
    private readonly ILogger<MonetizationStripeProvisioner> _logger;
    private readonly string _baseUrl;

    public MonetizationStripeProvisioner(
        HttpClient httpClient,
        IConfiguration configuration,
        IServiceToServiceTokenProvider tokenProvider,
        ILogger<MonetizationStripeProvisioner> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
        _baseUrl = (configuration.GetValue<string>("PaymentApi:BaseUrl") ?? string.Empty).Trim();
    }

    public async Task<MonetizationStripePlanResult> ProvisionPlanAsync(
        MonetizationStripePlanRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl))
        {
            return MonetizationStripePlanResult.CreateSkipped(request.PlanId, "PaymentApi:BaseUrl is not configured.");
        }

        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/api/internal/mozaiks/pay/monetization/price");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            message.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);
        }

        message.Content = JsonContent.Create(request);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Stripe provisioning failed status={StatusCode} planId={PlanId} body={Body}",
                (int)response.StatusCode,
                request.PlanId,
                body);

            return MonetizationStripePlanResult.Failed(request.PlanId, $"stripe provisioning failed ({(int)response.StatusCode})");
        }

        var payload = await response.Content.ReadFromJsonAsync<MonetizationStripePlanResponse>(cancellationToken: cancellationToken);
        if (payload == null || !payload.Succeeded)
        {
            return MonetizationStripePlanResult.Failed(request.PlanId, payload?.Error ?? "stripe provisioning failed");
        }

        return new MonetizationStripePlanResult
        {
            PlanId = request.PlanId,
            Succeeded = true,
            StripeProductId = payload.StripeProductId,
            StripePriceId = payload.StripePriceId,
            StripeLookupKey = payload.StripeLookupKey
        };
    }
}

public sealed class MonetizationStripePlanRequest
{
    public string AppId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "usd";
    public string BillingInterval { get; set; } = "month";
    public int SpecVersion { get; set; }
    public string SpecHash { get; set; } = string.Empty;
    public string? ProposalId { get; set; }
}

public sealed class MonetizationStripePlanResponse
{
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public string? StripeProductId { get; set; }
    public string? StripePriceId { get; set; }
    public string? StripeLookupKey { get; set; }
}

public sealed class MonetizationStripePlanResult
{
    public string PlanId { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public bool Skipped { get; set; }
    public string? Error { get; set; }
    public string? StripeProductId { get; set; }
    public string? StripePriceId { get; set; }
    public string? StripeLookupKey { get; set; }

    public static MonetizationStripePlanResult CreateSkipped(string planId, string reason)
        => new() { PlanId = planId, Skipped = true, Error = reason };

    public static MonetizationStripePlanResult Failed(string planId, string error)
        => new() { PlanId = planId, Succeeded = false, Error = error };
}


