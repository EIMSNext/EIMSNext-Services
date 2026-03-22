using EIMSNext.Entity;

namespace EIMSNext.CloudEvent
{
    public interface IEventHub
    {
        Task SendAsync(Webhook webhook, WebHookTrigger trigger, object data);
    }
}
