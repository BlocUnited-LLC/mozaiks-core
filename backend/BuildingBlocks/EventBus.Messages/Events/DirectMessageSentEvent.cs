using System;

namespace EventBus.Messages.Events;

public sealed class DirectMessageSentEvent : BaseEvents
{
    public string AppId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;

    public string SenderUserId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;

    public string RecipientUserId { get; set; } = string.Empty;

    public string ContentPreview { get; set; } = string.Empty;
}