using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public class WfCopyToNode : WfNodeAsyncBase<WfCopyToNode>
    {
        public WfCopyToNode(IResolver resolver) : base(resolver)
        {
        }

        public override Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            throw new NotImplementedException();
        }
    }
}
