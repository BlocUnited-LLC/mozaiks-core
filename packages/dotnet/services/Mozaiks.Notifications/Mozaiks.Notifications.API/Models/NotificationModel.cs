using Notification.API.Enums;

namespace Notification.API.Models
{
    public class NotificationModel: DocumentBase
    {
        public required string RecepientId { get; set; }
        public required string AppId { get; set; }
        public NotificationType Type { get; set; }
        public string Email { get; set; } = "";
        public string Subject { get; set; } = "";
        public required string Message { get; set; }
        public NotificationStatus Status { get; set; }
    }
}
