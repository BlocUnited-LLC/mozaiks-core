namespace AuthServer.Api.DTOs;

public sealed class DeployActionRequest
{
    public string AppId { get; set; } = string.Empty;
    public string? BuildId { get; set; }
}

