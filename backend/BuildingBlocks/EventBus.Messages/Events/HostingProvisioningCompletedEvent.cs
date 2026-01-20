namespace EventBus.Messages.Events;

public sealed class HostingProvisioningCompletedEvent : BaseEvents
{
    public string AppId { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ProvisioningJobId { get; set; } = string.Empty;

    public string JobType { get; set; } = string.Empty;

    public DateTime FinishedAtUtc { get; set; }

    public Dictionary<string, object?> Result { get; set; } = new();
}
