using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthServer.Api.Services;

public interface IServiceToServiceTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

public sealed class ServiceToServiceTokenProvider : IServiceToServiceTokenProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ServiceToServiceTokenProvider> _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private CachedToken? _cached;

    public ServiceToServiceTokenProvider(HttpClient httpClient, ILogger<ServiceToServiceTokenProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null && DateTimeOffset.UtcNow < _cached.RefreshAfterUtc)
        {
            return _cached.AccessToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null && DateTimeOffset.UtcNow < _cached.RefreshAfterUtc)
            {
                return _cached.AccessToken;
            }

            var clientId = RequireEnv("S2S_CLIENT_ID");
            var clientSecret = RequireEnv("S2S_CLIENT_SECRET");

            var authority = RequireEnv("AUTH_AUTHORITY");
            var tenantId = Environment.GetEnvironmentVariable("AUTH_TENANT_ID")?.Trim();
            var audience = RequireEnv("AUTH_AUDIENCE");

            var tokenEndpoint = DeriveTokenEndpoint(authority, tenantId);
            var scope = DeriveClientCredentialsScope(audience);

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = scope
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to acquire client-credentials token (status={StatusCode}). Body={Body}",
                    (int)response.StatusCode,
                    json);

                throw new HttpRequestException($"Failed to acquire service token (status {(int)response.StatusCode}).");
            }

            var payload = JsonSerializer.Deserialize<TokenResponse>(json, SerializerOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken) || payload.ExpiresIn <= 0)
            {
                throw new InvalidOperationException("Token endpoint response was missing access_token/expires_in.");
            }

            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddSeconds(payload.ExpiresIn);
            _cached = new CachedToken(
                AccessToken: payload.AccessToken,
                RefreshAfterUtc: expiresAt.AddMinutes(-1));

            return payload.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string RequireEnv(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} must be configured for service-to-service authentication.");
        }

        return value.Trim();
    }

    private static string DeriveTokenEndpoint(string authority, string? tenantId)
    {
        var normalized = authority.Trim().TrimEnd('/');

        if (normalized.EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^"/v2.0".Length];
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return $"{normalized}/oauth2/v2.0/token";
        }

        if (uri.Host.EndsWith(".ciamlogin.com", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(uri.AbsolutePath) || string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)))
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new InvalidOperationException("AUTH_TENANT_ID must be configured for CIAM client-credentials token acquisition.");
            }

            normalized = $"{normalized}/{tenantId.Trim()}";
        }

        return $"{normalized}/oauth2/v2.0/token";
    }

    private static string DeriveClientCredentialsScope(string audience)
    {
        var normalized = audience.Trim().TrimEnd('/');
        if (normalized.EndsWith("/.default", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (Guid.TryParse(normalized, out _))
        {
            return $"api://{normalized}/.default";
        }

        return $"{normalized}/.default";
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset RefreshAfterUtc);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

