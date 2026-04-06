using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;

using RabbitMQ.Client;

namespace EIMSNext.Async.Core.Messaging
{
    public class TaskProducer
    {
        private readonly IConnection _connection;
        private const string DEFAULT_QUEUE = "default";

        public TaskProducer(IConnection rabbitConnection) => _connection = rabbitConnection;

        public void Enqueue<T>(Expression<Action<T>> task, string? explicitQueue = null)
        {
            var methodCall = (MethodCallExpression)task.Body;
            var methodInfo = methodCall.Method;
            var classType = typeof(T);

            // 队列决策链：显式参数 > 方法Attribute > 类Attribute > default
            string resolvedQueue = (explicitQueue
                ?? methodInfo.GetCustomAttribute<QueueAttribute>()?.QueueName
                ?? classType.GetCustomAttribute<QueueAttribute>()?.QueueName
                ?? DEFAULT_QUEUE).ToLower();

            // 构建消息
            var args = methodCall.Arguments.Select(a =>
                Expression.Lambda(a).Compile().DynamicInvoke()).ToArray();

            var message = new TaskMessage
            {
                TaskType = $"{classType.FullName},{classType.Assembly.GetName().Name}",
                ArgumentsJson = JsonSerializer.Serialize(new
                {
                    Method = methodInfo.Name,
                    Parameters = args
                })
            };

            using var channel = _connection.CreateModel();
            channel.QueueDeclare(resolvedQueue, durable: true);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            channel.BasicPublish("", resolvedQueue, null, body);
        }
    }
}
