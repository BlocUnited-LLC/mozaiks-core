namespace EventBus.Messages.Events;

public sealed class HostingProvisioningRequestedEvent : BaseEvents
{
    public string AppId { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string? AppName { get; set; }

    public string JobType { get; set; } = "ProvisionAll";

    public string? Provider { get; set; }

    public Dictionary<string, object?> Payload { get; set; } = new();
}
