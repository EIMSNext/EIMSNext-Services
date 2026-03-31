using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Async.Core.Messaging
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class QueueAttribute : Attribute
    {
        public string QueueName { get; }
        public QueueAttribute(string queueName) => QueueName = queueName;
    }
}
