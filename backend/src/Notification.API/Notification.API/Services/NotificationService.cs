using Microsoft.AspNetCore.SignalR;
using Notification.API.DTOs;
using Notification.API.Enums;
using Notification.API.Helpers;
using Notification.API.Models;
using Notification.API.Repository.Contract;
using System.Security.Claims;

namespace Notification.API.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly INotificationRepository _repository;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(
            INotificationRepository repository,
            IHttpContextAccessor httpContextAccessor,
            IHubContext<NotificationHub> hubContext)
        {
            _repository = repository;
            _httpContextAccessor = httpContextAccessor;
            _hubContext = hubContext;
        }

        public async Task<NotificationModel> CreateAsync(NotificationModel notification, NotificationType type)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userName = user?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "Mozaiks";

            notification.Type = type;
            notification.Status = NotificationStatus.Sent;

            var result = await _repository.CreateAsync(notification);
            if (result.Id == null)
            {
                return result;
            }

            switch (type)
            {
                case NotificationType.Email:
                    var emailResult = await SendGridHelper.SendEmailAsync(notification.Email, userName, notification.Subject, notification.Message);
                    if (emailResult.Contains("successfully"))
                    {
                        notification.Status = NotificationStatus.Sent;
                        await _repository.UpdateAsync(notification);
                    }

                    break;
                case NotificationType.Push:
                    await _hubContext.Clients.Group(UserGroup(notification.RecepientId))
                        .SendAsync("ReceiveNotification", ToDto(notification));
                    break;
            }

            return result;
        }

        public Task<IEnumerable<NotificationModel>> GetAllUserNotificationAsync(string userId, string appId)
            => _repository.GetAllAsync(userId, appId);

        public Task<NotificationModel> GetByIdAsync(string id)
            => _repository.GetByIdAsync(id);

        public Task UpdateAsync(NotificationModel notification)
            => _repository.UpdateAsync(notification);

        private static string UserGroup(string userId) => $"user:{userId}";

        private static NotificationDto ToDto(NotificationModel notification)
        {
            return new NotificationDto
            {
                Id = notification.Id ?? string.Empty,
                AppId = notification.AppId,
                RecepientId = notification.RecepientId,
                Message = notification.Message,
                Type = notification.Type.ToString(),
                Status = notification.Status.ToString(),
                CreatedAtUtc = notification.CreatedAt
            };
        }
    }
}

