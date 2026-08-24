using EIMSNext.Service.Entities;

namespace EIMSNext.CloudEvent
{
    public interface IEventHub
    {
        Task SendAsync(Webhook webhook, WebHookTrigger trigger, string eventId, object data);
    }
}
