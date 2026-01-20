using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthServer.Api.Models;
using Microsoft.Extensions.Options;

namespace AuthServer.Api.Services;

public sealed class ProvisioningAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ProvisioningAgentOptions _options;
    private readonly ILogger<ProvisioningAgentClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ProvisioningAgentClient(
        HttpClient httpClient,
        IOptions<ProvisioningAgentOptions> options,
        ILogger<ProvisioningAgentClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public async Task<ProvisioningResponse> ProvisionDatabaseAsync(string appId, string databaseName, string? schemaJson, string? seedJson, CancellationToken cancellationToken)
    {
         if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("ProvisioningAgent:BaseUrl must be configured.");
        }

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/provision";
        var jobId = Guid.NewGuid().ToString();

        var payload = new Dictionary<string, object?>
        {
            ["databaseName"] = databaseName,
            ["schemaJson"] = schemaJson,
            ["seedJson"] = seedJson
        };

        var requestBody = new ProvisioningAgentRequest
        {
            JobId = jobId,
            AppId = appId,
            Type = "ProvisionDatabase",
            Payload = payload
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("X-Api-Key", _options.ApiKey);
        }

        // We likely want synchronous response for database provisioning to get the connection string immediately
        // unless the agent forces async. The prompt says "API returns a ProvisioningResponse", implies sync-ish or immediate result.
        // Prompt Requirement 3: "The API returns a ProvisioningResponse containing the secure connection string."
        // So we do NOT send X-Async: true here by default for DB provision if we want the result body immediately.
        // BUT Requirement 3 says "Include header X-Async: true for all deployment jobs."
        // This is a "Database Integration" task, maybe it behaves differently. 
        // "Do NOT attempt to connect to MongoDB directly from here. Call the Provisioning Agent instead."
        
        // I will assume for "ProvisionDatabase" we want a response with the connection string, so I will NOT set X-Async unless configured/requested.
        // If the agent is async-only, it will return a JobId and we'd have to poll. 
        // But "The API returns a ProvisioningResponse containing..." suggests immediate return.
        
        _logger.LogInformation("Calling Provisioning Agent for DB: appId={AppId} endpoint={Endpoint}", appId, endpoint);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Provisioning Agent failed: {StatusCode} {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Provisioning Agent failed: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<ProvisioningResponse>(JsonOptions, cancellationToken);
        return result ?? new ProvisioningResponse();
    }

    private sealed class ProvisioningAgentRequest
    {
        [JsonPropertyName("jobId")]
        public string JobId { get; set; } = string.Empty;

        [JsonPropertyName("appId")]
        public string AppId { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public Dictionary<string, object?> Payload { get; set; } = new();
    }
}

public class ProvisioningResponse
{
    public bool Success { get; set; }
    public string? JobId { get; set; }
    public string? ErrorMessage { get; set; }
    public ProvisioningUpdate? Update { get; set; }
}

public class ProvisioningUpdate
{
    public MongoUpdate? Mongo { get; set; }
}

public class MongoUpdate
{
    public string? ConnectionStringSecretRef { get; set; }
    public string? DatabaseName { get; set; }
}
