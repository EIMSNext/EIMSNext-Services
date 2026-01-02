using HKH.Mef2.Integration;
using MongoDB.Driver;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public class WfCopyToNode : WfNodeAsyncBase<WfCopyToNode>
    {
        public WfCopyToNode(IResolver resolver) : base(resolver)
        {
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);

            var empIds = await PopulateEmpIds(dataContext, Metadata!.WfNodeSetting?.CopyToSetting?.Candidates);

            if (empIds.Any())
                await AddCCLogs(context.Workflow, dataContext, Metadata!, empIds, null);

            return ExecutionResult.Next();
        }
    }
}
