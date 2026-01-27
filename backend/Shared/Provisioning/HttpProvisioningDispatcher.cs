using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hosting.API.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Hosting.API.Services.Provisioning;

public sealed class HttpProvisioningDispatcher : IProvisioningDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ProvisioningAgentOptions _options;
    private readonly ILogger<HttpProvisioningDispatcher> _logger;

    public HttpProvisioningDispatcher(
        HttpClient httpClient,
        IOptions<ProvisioningAgentOptions> options,
        ILogger<HttpProvisioningDispatcher> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public async Task DispatchAsync(ProvisioningJob job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("ProvisioningAgent:BaseUrl (or Provisioning:Url) must be configured.");
        }

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/provision";

        var requestBody = BuildProvisioningRequest(job);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };

        // Add X-Api-Key header for authentication
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("X-Api-Key", _options.ApiKey);
        }

        // Add X-Async header for async processing
        if (_options.AsyncMode)
        {
            request.Headers.Add("X-Async", "true");
        }

        // Add correlation ID for tracing
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        request.Headers.Add("X-Correlation-Id", correlationId);

        // Alternate header for compatibility
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("X-Internal-Api-Key", _options.ApiKey);
        }

        _logger.LogInformation(
            "Dispatching provisioning job to agent: jobId={JobId} appId={AppId} type={Type} endpoint={Endpoint}",
            job.Id,
            job.AppId,
            job.Type,
            endpoint);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Provisioning dispatch failed ({(int)response.StatusCode}): {body}");
        }

        _logger.LogInformation(
            "Provisioning dispatch accepted jobId={JobId} appId={AppId} type={Type}",
            job.Id,
            job.AppId,
            job.Type);
    }

    private ProvisioningAgentRequest BuildProvisioningRequest(ProvisioningJob job)
    {
        return new ProvisioningAgentRequest
        {
            JobId = job.Id ?? Guid.NewGuid().ToString(),
            AppId = job.AppId,
            Type = job.Type.ToString(),
            Payload = NormalizePayload(job.Payload)
        };
    }

    /// <summary>
    /// Request structure sent to the Provisioning Agent.
    /// </summary>
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

    private static Dictionary<string, object?> NormalizePayload(Dictionary<string, object?> payload)
    {
        var normalized = new Dictionary<string, object?>(payload.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in payload)
        {
            normalized[key] = NormalizeValue(value);
        }

        return normalized;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is ObjectId objectId)
        {
            return objectId.ToString();
        }

        if (value is BsonObjectId bsonObjectId)
        {
            return bsonObjectId.Value.ToString();
        }

        if (value is BsonValue bsonValue)
        {
            if (bsonValue.IsObjectId)
            {
                return bsonValue.AsObjectId.ToString();
            }

            if (bsonValue.IsString)
            {
                return bsonValue.AsString;
            }

            if (bsonValue.IsBoolean)
            {
                return bsonValue.AsBoolean;
            }

            if (bsonValue.IsInt32)
            {
                return bsonValue.AsInt32;
            }

            if (bsonValue.IsInt64)
            {
                return bsonValue.AsInt64;
            }

            if (bsonValue.IsDouble)
            {
                return bsonValue.AsDouble;
            }
        }

        if (value is Dictionary<string, object?> dict)
        {
            return NormalizePayload(dict);
        }

        if (value is IDictionary<string, object?> dictInterface)
        {
            var normalizedDict = new Dictionary<string, object?>(dictInterface.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in dictInterface)
            {
                normalizedDict[entry.Key] = NormalizeValue(entry.Value);
            }

            return normalizedDict;
        }

        if (value is IEnumerable<object?> list)
        {
            return list.Select(NormalizeValue).ToList();
        }

        return value;
    }
}
