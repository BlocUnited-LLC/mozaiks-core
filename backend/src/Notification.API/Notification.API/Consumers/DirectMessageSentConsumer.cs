using EventBus.Messages.Events;
using MassTransit;
using Notification.API.Enums;
using Notification.API.Models;
using Notification.API.Services;

namespace Notification.API.Consumers;

public sealed class DirectMessageSentConsumer : IConsumer<DirectMessageSentEvent>
{
    private readonly INotificationService _notifications;
    private readonly ILogger<DirectMessageSentConsumer> _logger;

    public DirectMessageSentConsumer(
        INotificationService notifications,
        ILogger<DirectMessageSentConsumer> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DirectMessageSentEvent> context)
    {
        var message = context.Message;

        if (string.IsNullOrWhiteSpace(message.AppId) ||
            string.IsNullOrWhiteSpace(message.RecipientUserId))
        {
            _logger.LogWarning("DirectMessageSentEvent missing required fields appId={AppId} recipient={Recipient}", message.AppId, message.RecipientUserId);
            return;
        }

        var text = string.IsNullOrWhiteSpace(message.SenderName)
            ? "New message"
            : $"New message from {message.SenderName}";

        if (!string.IsNullOrWhiteSpace(message.ContentPreview))
        {
            text = $"{text}: {message.ContentPreview}";
        }

        var notification = new NotificationModel
        {
            AppId = message.AppId,
            RecepientId = message.RecipientUserId,
            Message = text,
            Subject = "New message",
            Email = string.Empty
        };

        await _notifications.CreateAsync(notification, NotificationType.Push);

        _logger.LogInformation(
            "Created notification from direct message event appId={AppId} convoId={ConversationId} msgId={MessageId} recipient={Recipient}",
            message.AppId,
            message.ConversationId,
            message.MessageId,
            message.RecipientUserId);
    }
}
