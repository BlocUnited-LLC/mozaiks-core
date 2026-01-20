
using Notification.API.Models;

namespace Notification.API.Repository.Contract
{
    public interface INotificationRepository
    {
        Task<NotificationModel> CreateAsync(NotificationModel notification);
        Task<NotificationModel> GetByIdAsync(string id);
        Task<IEnumerable<NotificationModel>> GetAllAsync(string userId, string appId);
        Task UpdateAsync(NotificationModel notification);
        Task DeleteAsync(string id);
    }
}
