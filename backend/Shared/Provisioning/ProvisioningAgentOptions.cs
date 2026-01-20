namespace Hosting.API.Services.Provisioning;

public sealed class ProvisioningAgentOptions
{
    /// <summary>
    /// Base URL for the provisioning agent (e.g., "http://provisioning-agent:8080").
    /// Can also be configured via "Provisioning:Url".
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Alias for BaseUrl - allows configuration via "Provisioning:Url".
    /// </summary>
    public string Url
    {
        get => BaseUrl;
        set => BaseUrl = value;
    }

    /// <summary>
    /// API key sent in X-Api-Key header for agent authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Legacy key name - alias for ApiKey.
    /// </summary>
    public string InternalApiKey
    {
        get => ApiKey;
        set => ApiKey = value;
    }

    /// <summary>
    /// HTTP timeout in seconds. Default is 60.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to send X-Async: true header for async job processing.
    /// Default is true - agent processes jobs in background and calls back.
    /// </summary>
    public bool AsyncMode { get; set; } = true;
}
