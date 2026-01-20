using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.Messages.Events
{
    public class BaseEvents
    {
        public Guid Id { get;  set; } = Guid.NewGuid();
        public DateTime CreatedAt { get;  set; } = DateTime.UtcNow;
    }
}
