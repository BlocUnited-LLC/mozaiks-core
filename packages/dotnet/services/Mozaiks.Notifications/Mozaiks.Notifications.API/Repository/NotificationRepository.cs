using MongoDB.Driver;
using Notification.API.Models;
using Notification.API.Repository.Contract;

namespace Notification.API.Repository
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IMongoCollection<NotificationModel> _notifications;

        public NotificationRepository(IMongoDatabase database)
        {
            _notifications = database.GetCollection<NotificationModel>("Notifications");
        }

        public async Task<NotificationModel> CreateAsync(NotificationModel notification)
        {
            await _notifications.InsertOneAsync(notification);
            return notification;
        }

        public async Task<NotificationModel> GetByIdAsync(string id)
        {
            return await _notifications.Find(n => n.Id == id).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<NotificationModel>> GetAllAsync(string userId, string appId)
        {
            var filter = Builders<NotificationModel>.Filter.Eq(x => x.RecepientId, userId)
                         & Builders<NotificationModel>.Filter.Eq(x => x.AppId, appId)
                         & Builders<NotificationModel>.Filter.Ne(x => x.Status, NotificationStatus.Deleted);

            return await _notifications.Find(filter).SortByDescending(x => x.CreatedAt).ToListAsync();
        }

        public async Task UpdateAsync(NotificationModel notification)
        {
            await _notifications.ReplaceOneAsync(n => n.Id == notification.Id, notification);
        }

        public async Task DeleteAsync(string id)
        {
            await _notifications.DeleteOneAsync(n => n.Id == id);
        }
    }
}
