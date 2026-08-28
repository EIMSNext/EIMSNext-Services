using EIMSNext.Entities;

namespace EIMSNext.Notification
{
    public interface IEventHub
    {
        Task SendAsync(Webhook webhook, WebHookTrigger trigger, string eventId, object data);
    }
}
