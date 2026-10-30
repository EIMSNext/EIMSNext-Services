using EIMSNext.ApiClient.Flow;
using EIMSNext.Async.Tasks.Consumers;

namespace EIMSNext.Async.Tests;

[TestClass]
public class EventFlowRunConsumerFailureClassificationTests
{
    [TestMethod]
    [DataRow("ServiceUnavailable")]
    [DataRow("503")]
    [DataRow("TooManyRequests")]
    [DataRow("429")]
    public void FlowHostInfrastructureFailure_IsRetried(string responseId)
    {
        Assert.IsTrue(EventFlowRunConsumer.IsTransientFlowHostFailure(new WfResponse
        {
            Id = responseId,
            Error = "temporary failure"
        }));
    }

    [TestMethod]
    public void FlowBusinessFailure_IsNotRetried()
    {
        Assert.IsFalse(EventFlowRunConsumer.IsTransientFlowHostFailure(new WfResponse
        {
            Id = "BadRequest",
            Error = "EF validation failed"
        }));
    }
}
