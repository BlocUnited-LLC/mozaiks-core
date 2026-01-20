using System.Net.Http;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Mozaiks.Auth;

internal sealed class JwksOpenIdConnectConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
{
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromHours(6);

    private readonly HttpClient _httpClient;
    private readonly string _issuer;
    private readonly string _jwksUrl;
    private readonly TimeSpan _refreshInterval;

    private readonly object _lock = new();
    private OpenIdConnectConfiguration? _cachedConfiguration;
    private DateTimeOffset _refreshAfterUtc = DateTimeOffset.MinValue;

    public JwksOpenIdConnectConfigurationManager(
        HttpClient httpClient,
        string issuer,
        string jwksUrl,
        TimeSpan? refreshInterval = null)
    {
        _httpClient = httpClient;
        _issuer = issuer;
        _jwksUrl = jwksUrl;
        _refreshInterval = refreshInterval ?? DefaultRefreshInterval;
    }

    public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        if (TryGetFresh(out var configuration))
        {
            return Task.FromResult(configuration);
        }

        return FetchAndCacheAsync(cancel);
    }

    public void RequestRefresh()
    {
        lock (_lock)
        {
            _refreshAfterUtc = DateTimeOffset.MinValue;
        }
    }

    private bool TryGetFresh(out OpenIdConnectConfiguration configuration)
    {
        lock (_lock)
        {
            if (_cachedConfiguration is not null && DateTimeOffset.UtcNow < _refreshAfterUtc)
            {
                configuration = _cachedConfiguration;
                return true;
            }
        }

        configuration = null!;
        return false;
    }

    private async Task<OpenIdConnectConfiguration> FetchAndCacheAsync(CancellationToken cancel)
    {
        var json = await _httpClient.GetStringAsync(_jwksUrl, cancel);
        var jwks = new JsonWebKeySet(json);

        if (jwks.Keys.Count == 0)
        {
            throw new InvalidOperationException($"JWKS document '{_jwksUrl}' contained no keys.");
        }

        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = _issuer
        };

        foreach (var key in jwks.Keys)
        {
            configuration.SigningKeys.Add(key);
        }

        lock (_lock)
        {
            _cachedConfiguration = configuration;
            _refreshAfterUtc = DateTimeOffset.UtcNow.Add(_refreshInterval);
        }

        return configuration;
    }
}

