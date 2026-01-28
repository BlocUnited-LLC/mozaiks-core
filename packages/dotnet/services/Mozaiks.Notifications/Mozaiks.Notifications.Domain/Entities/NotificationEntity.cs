using Notification.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notification.Domain.Entities
{
    public class NotificationEntity : EntityBase
    {
        // User to whom the notification is targeted
        public string ToUserId { get; set; }
        // The content or message of the notification
        public string Title { get; set; }

        // URL or link associated with the notification (e.g., a link to a specific resource or detailed de)
        public string Message { get; set; }

        // Optional: Timestamp indicating when the notification was read by the user
        public DateTime? ReadAt { get; set; }

        // Type of notification (e.g., "message," "alert," "update")
        public string Type { get; set; }

        // Flag indicating whether the notification is marked as important 1 = Important, 2 = Less Important
        public int Priority { get; set; }

        // Additional metadata or custom properties as needed
        public Dictionary<string, object> Metadata { get; set; }
    }
}
