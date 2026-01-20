namespace AuthServer.Api.Models;

public sealed class ProvisioningAgentOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// If true, sends X-Async: true header to agent for async processing.
    /// </summary>
    public bool AsyncMode { get; set; } = true;
}
