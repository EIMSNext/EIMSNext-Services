using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IFormNotifyTaskPublisher
    {
        void Publish(FormNotifyDispatchTaskArgs args);
    }
}
