using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Async.Core.Messaging
{
    public record TaskMessage
    {
        public string TaskType { get; init; } = string.Empty;
        public string ArgumentsJson { get; init; } = string.Empty;
        public DateTime EnqueuedAt { get; init; } = DateTime.UtcNow;
    }
}
