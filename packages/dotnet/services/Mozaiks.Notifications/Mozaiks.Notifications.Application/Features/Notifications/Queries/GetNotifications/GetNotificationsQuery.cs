using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notification.Application.Features.Notifications.Queries.GetNotifications
{
    public class GetNotificationsQuery : IRequest
    {
        public string UserId { get; set; }

        GetNotificationsQuery(string userId)
        {
            UserId = userId;
        }
    }
}
