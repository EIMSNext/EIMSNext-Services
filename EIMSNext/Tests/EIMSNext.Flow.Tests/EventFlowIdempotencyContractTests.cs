using System.Dynamic;
using System.Text.Json;
using EIMSNext.Entities;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Interfaces;

namespace EIMSNext.Flow.Tests
{
    [TestClass]
    public class EventFlowIdempotencyContractTests
    {
        [TestMethod]
        public void NodeSnapshotRoundTripPreservesPersistedResult()
        {
            var node = new EfNodeData
            {
                NodeId = "insert-1",
                FormId = "form-1",
                SingleResult = true,
                ActionDatas =
                [
                    new ActionFormData
                    {
                        State = DataState.Inserted,
                        Persisted = true,
                        FormData = new FormData
                        {
                            Id = "data-1",
                            FormId = "form-1",
                            Data = new Dictionary<string, object?>()
                        }
                    }
                ]
            };

            var restored = node.SerializeToJson().DeserializeFromJson<EfNodeData>();

            Assert.IsNotNull(restored);
            Assert.AreEqual("insert-1", restored.NodeId);
            Assert.AreEqual("data-1", restored.ActionDatas.Single().FormData.Id);
            Assert.IsTrue(restored.ActionDatas.Single().Persisted);
        }


        private static EfRunParameter NewParameter()
        {
            return new EfRunParameter(
                "user-1",
                "token",
                new FormData { Id = "data-1", FormId = "form-1", Data = new Dictionary<string, object?>() },
                EventSourceType.Form,
                EventType.Approving,
                "node-1",
                null).WithNodeAction("submit");
        }
    }
}
