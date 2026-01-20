using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AuthServer.Api.Services;

/// <summary>
/// Lightweight response from Hosting.API for lifecycle resolution.
/// </summary>
public sealed class HostedAppStatusResponse
{
    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("hostingUrl")]
    public string? HostingUrl { get; set; }

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }
}

public sealed class ProvisioningJobResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("lastError")]
    public string? LastError { get; set; }
}

public sealed class HostedAppWithJobsResponse
{
    public HostedAppStatusResponse? HostedApp { get; set; }
    public List<ProvisioningJobResponse> ProvisioningJobs { get; set; } = new();
}

/// <summary>
/// Client for fetching hosting status from Hosting.API.
/// Used by AppLifecyclePhaseResolver to compute canonical phase.
/// </summary>
public interface IHostingApiClient
{
    /// <summary>
    /// Gets the hosted app status and active provisioning jobs for an app.
    /// </summary>
    Task<HostedAppWithJobsResponse?> GetHostedAppStatusAsync(string appId, CancellationToken cancellationToken);
}

public sealed class HostingApiClient : IHostingApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HostingApiClient> _logger;

    public HostingApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HostingApiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HostedAppWithJobsResponse?> GetHostedAppStatusAsync(string appId, CancellationToken cancellationToken)
    {
        var baseUrl = _configuration.GetValue<string>("HostingApi:BaseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("HostingApi:BaseUrl not configured - cannot fetch hosted app status");
            return null;
        }

        try
        {
            // Fetch hosted app status
            var hostedAppUrl = $"{baseUrl.TrimEnd('/')}/api/internal/apps/{appId}/status";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, hostedAppUrl);
            
            var apiKey = _configuration.GetValue<string>("HostingApi:InternalApiKey");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Add("X-Internal-Api-Key", apiKey);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // No hosted app record - this is normal for Draft/Building/Preview phases
                    return new HostedAppWithJobsResponse();
                }
                
                _logger.LogWarning(
                    "Failed to fetch hosted app status for appId={AppId}: {StatusCode}",
                    appId,
                    response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<HostedAppWithJobsResponse>(cancellationToken: cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching hosted app status for appId={AppId}", appId);
            return null;
        }
    }
}
