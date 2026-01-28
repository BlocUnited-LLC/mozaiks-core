namespace Notification.API.Email.Models;

public class EmailTemplate
{
    public string Id { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty; // e.g., "welcome", "password_reset"
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class EmailRecipient
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Variables { get; set; } = new();
}

public class EmailSendRequest
{
    public string AppId { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public EmailRecipient Recipient { get; set; } = new();
    public Dictionary<string, string> GlobalVariables { get; set; } = new();
    public string? CustomSubject { get; set; }
    public string? ReplyTo { get; set; }
}

public class EmailSendResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? Error { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class EmailLog
{
    public string Id { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public string? SendGridMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClickedAt { get; set; }
}

public enum EmailStatus
{
    Pending,
    Sent,
    Delivered,
    Opened,
    Clicked,
    Bounced,
    Failed
}

public enum TransactionalEmailType
{
    Welcome,
    EmailVerification,
    PasswordReset,
    PasswordChanged,
    SubscriptionCreated,
    SubscriptionUpdated,
    SubscriptionCancelled,
    PaymentReceived,
    PaymentFailed,
    TeamInvite,
    AppStatusChange,
    SecurityAlert
}
