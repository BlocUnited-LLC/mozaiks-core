using Notification.API.Enums;
using Notification.API.Models;

namespace Notification.API.Services
{
    public interface INotificationService
    {
        Task<NotificationModel> CreateAsync(NotificationModel notification, NotificationType type);
        Task<NotificationModel> GetByIdAsync(string id);
        Task<IEnumerable<NotificationModel>> GetAllUserNotificationAsync(string userId, string appId);
        Task UpdateAsync(NotificationModel notification);
    }
}
