using EIMSNext.Async.Core.Messaging.Consumers;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

namespace EIMSNext.Async.Core.Messaging
{
    public class FormNotifyTaskPublisher(TaskProducer producer) : IFormNotifyTaskPublisher
    {
        public void Publish(FormNotifyDispatchTaskArgs args)
        {
            producer.Enqueue<FormNotifyDispatchConsumer>(x => x.Enqueue(args));
        }
    }
}
