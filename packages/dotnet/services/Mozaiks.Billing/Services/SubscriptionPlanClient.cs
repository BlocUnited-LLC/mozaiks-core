using System.Net.Http.Json;

namespace Payment.API.Services
{
    public sealed class SubscriptionPlanClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SubscriptionPlanClient> _logger;

        public SubscriptionPlanClient(HttpClient httpClient, IConfiguration configuration, ILogger<SubscriptionPlanClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            var baseUrl = configuration.GetValue<string>("AuthApi:BaseUrl");
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
        }

        public async Task<SubscriptionPlanSnapshot?> GetPlanAsync(string planId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(planId))
            {
                return null;
            }

            try
            {
                var response = await _httpClient.GetAsync($"/api/SubscriptionPlan/{Uri.EscapeDataString(planId)}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Plan lookup failed (status={StatusCode}) for planId={PlanId}", response.StatusCode, planId);
                    return null;
                }

                var dto = await response.Content.ReadFromJsonAsync<SubscriptionPlanDto>(cancellationToken: cancellationToken);
                if (dto == null)
                {
                    return null;
                }

                return new SubscriptionPlanSnapshot(dto.Name ?? string.Empty, dto.Price);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plan lookup failed for planId={PlanId}", planId);
                return null;
            }
        }

        public sealed record SubscriptionPlanSnapshot(string Name, decimal Price);

        private sealed class SubscriptionPlanDto
        {
            public string? Name { get; set; }
            public decimal Price { get; set; }
        }
    }
}

