using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace AuthServer.Api.Services;

public sealed class MozaiksPayClient
{
    private readonly HttpClient _httpClient;
    private readonly IServiceToServiceTokenProvider _tokenProvider;
    private readonly ILogger<MozaiksPayClient> _logger;

    public MozaiksPayClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IServiceToServiceTokenProvider tokenProvider,
        ILogger<MozaiksPayClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;

        var baseUrl = (configuration.GetValue<string>("PaymentApi:BaseUrl") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        }
    }

    public async Task<MozaiksPayStatusSnapshot> GetSubscriptionStatusAsync(
        string userId,
        string scope,
        string? appId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("userId is required", nameof(userId));
        }

        var qs = new Dictionary<string, string?>
        {
            ["userId"] = userId,
            ["scope"] = scope,
            ["appId"] = appId
        };

        var uri = QueryHelpers.AddQueryString("/api/internal/mozaiks/pay/subscription-status", qs);

        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "MozaiksPay subscription-status failed status={StatusCode} scope={Scope} appId={AppId} body={Body}",
                (int)response.StatusCode,
                scope,
                appId,
                body);

            throw new HttpRequestException($"MozaiksPay subscription-status failed ({(int)response.StatusCode})");
        }

        var dto = await response.Content.ReadFromJsonAsync<MozaiksPayStatusResponse>(cancellationToken: cancellationToken);
        if (dto is null)
        {
            return new MozaiksPayStatusSnapshot(false, null, null, Array.Empty<string>(), null, null);
        }

        return new MozaiksPayStatusSnapshot(
            dto.IsActive,
            dto.PlanId,
            dto.ExpiresAtUtc,
            dto.Features ?? new List<string>(),
            dto.SubscriptionId,
            dto.CurrentPeriodEndUtc);
    }

    public sealed record MozaiksPayStatusSnapshot(
        bool IsActive,
        string? PlanId,
        DateTime? ExpiresAtUtc,
        IReadOnlyList<string> Features,
        string? SubscriptionId,
        DateTime? CurrentPeriodEndUtc);

    private sealed class MozaiksPayStatusResponse
    {
        public bool IsActive { get; set; }
        public string? PlanId { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string? SubscriptionId { get; set; }
        public DateTime? CurrentPeriodEndUtc { get; set; }
        public List<string>? Features { get; set; }
    }
}
