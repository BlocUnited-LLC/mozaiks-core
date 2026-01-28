namespace Notification.API.ViewModels
{
    public class UserNotifications
    {
        public string UserId { get; set; }
        public string RecepientId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string AppId { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
