namespace Notification.API.DTOs;

public sealed class AppBuildEventRequest
{
    public string AppId { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public string? AppName { get; set; }
    public string BuildId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // built | error
}

