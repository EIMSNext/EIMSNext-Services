using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Service.Entities;
using EIMSNext.Async.RabbitMQ.Outbox;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class OutboxIdempotencyKeyFactoryTests
    {
        private readonly OutboxIdempotencyKeyFactory _factory = new();

        [TestMethod]
        public void Create_ExternalMessages_ReturnsStableBusinessKeys()
        {
            Assert.AreEqual("email:notify-1:123", _factory.Create(new EmailNotifyTaskArgs { NotifyId = "notify-1", EventStamp = 123 }));
            Assert.AreEqual("system-message:notify-1:123", _factory.Create(new SystemMessageTaskArgs { NotifyId = "notify-1", EventStamp = 123 }));
            Assert.AreEqual(
                $"webhook:corp-1:app-1:form-1::Data_Updated:event-1",
                _factory.Create(new WebhookTaskArgs
                {
                    CorpId = "corp-1",
                    AppId = "app-1",
                    FormId = "form-1",
                    Trigger = WebHookTrigger.Data_Updated,
                    EventId = "event-1"
                }));
        }

        [TestMethod]
        public void Create_InternalCommand_IsRejectedAtCompileTime()
        {
            Assert.IsFalse(typeof(IOutboxMessage).IsAssignableFrom(typeof(DataExportTaskArgs)));
        }

        [TestMethod]
        public void Create_WebhookWithoutEventId_Throws()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => _factory.Create(new WebhookTaskArgs()));
        }
    }
}
