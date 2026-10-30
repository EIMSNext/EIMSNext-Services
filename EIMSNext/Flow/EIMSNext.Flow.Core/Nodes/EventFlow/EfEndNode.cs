using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EfEndNode : EfNodeBase<EfEndNode>
    {
        public EfEndNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLogAsync(context, _ => Task.FromResult(ExecutionResult.Next())).GetAwaiter().GetResult();
        }
    }
}
