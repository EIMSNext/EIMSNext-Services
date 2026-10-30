using EIMSNext.Async.Abstractions.Messaging;

namespace EIMSNext.Async.RabbitMQ.Outbox
{
    public sealed class OutboxIdempotencyKeyFactory : IOutboxIdempotencyKeyFactory
    {
        public string Create<TMessage>(TMessage message) where TMessage : class, IOutboxMessage
        {
            return message switch
            {
                EmailNotifyTaskArgs email when email.EventStamp > 0 => $"email:{email.NotifyId}:{email.EventStamp}",
                SystemMessageTaskArgs system when system.EventStamp > 0 => $"system-message:{system.NotifyId}:{system.EventStamp}",
                WebhookTaskArgs webhook when !string.IsNullOrWhiteSpace(webhook.EventId)
                    => $"webhook:{webhook.CorpId}:{webhook.AppId}:{webhook.FormId}:{webhook.DataId}:{webhook.Trigger}:{webhook.EventId}",
                EventFlowRunTaskArgs eventFlow when !string.IsNullOrWhiteSpace(eventFlow.RetryAttemptId)
                    => $"eventflow-retry:{eventFlow.CorpId}:{eventFlow.RetryAttemptId}",
                EventFlowRunTaskArgs eventFlow when !string.IsNullOrWhiteSpace(eventFlow.ExecutionId)
                    => $"eventflow:{eventFlow.CorpId}:{eventFlow.ExecutionId}",
                _ => throw new InvalidOperationException($"{typeof(TMessage).Name} requires a stable external event key.")
            };
        }
    }
}
