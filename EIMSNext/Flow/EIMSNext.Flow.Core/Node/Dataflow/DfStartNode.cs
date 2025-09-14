using HKH.Mef2.Integration;

using EIMSNext.Entity;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public class DfStartNode : DfNodeBase<DfStartNode>
    {
        public DfStartNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);

            if (!string.IsNullOrEmpty(dataContext.DataId) && !dataContext.NodeDatas.ContainsKey(Metadata!.Id))
            {
                dataContext.NodeDatas.Add(Metadata!.Id, new DfNodeData
                {
                    NodeId = Metadata.Id,
                    SingleResult = Metadata.DfNodeSetting!.SingleResult,
                    FormId = dataContext.FormId,
                    ActionDatas = new List<ActionFormData>() { new ActionFormData { State = DataState.Unchanged, FormData = GetFormData(dataContext.DataId)! } }
                });
            }

            return ExecutionResult.Next();
        }
    }
}
