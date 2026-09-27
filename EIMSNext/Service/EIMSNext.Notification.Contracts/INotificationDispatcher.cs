namespace EIMSNext.Notification.Contracts;

public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

public sealed record NotificationMessage(string CorpId, string Channel, string Payload);
