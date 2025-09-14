using System.Dynamic;

using EIMSNext.Entity;

using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;

namespace EIMSNext.Flow.Core.Node
{
    public class WfDecideNode : Decide, IFlowNode
    {
        public WfStep? Metadata { get; set; }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            if (context.Workflow.Data is DfDataContext dfDataContext)
            {
                dfDataContext.MatchedResult = false;
                dfDataContext.MatchParallel = Metadata!.NodeType== WfNodeType.Branch;
            }
            else
            {
                var wfDataContext = (ExpandoObject)context.Workflow.Data;
                wfDataContext.AddOrUpdate(WfConsts.MatchedResult, false);
                wfDataContext.AddOrUpdate(WfConsts.MatchParallel, Metadata!.NodeType == WfNodeType.Branch);
            }

            return ExecutionResult.Outcome(Expression);
        }
    }
}
