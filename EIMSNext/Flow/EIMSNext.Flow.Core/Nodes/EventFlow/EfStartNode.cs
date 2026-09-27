using HKH.Mef2.Integration;

using EIMSNext.Entities;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EfStartNode : EfNodeBase<EfStartNode>
    {
        public EfStartNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLog(context, dataContext =>
            {
                if (!string.IsNullOrEmpty(dataContext.DataId) && !dataContext.NodeDatas.ContainsKey(Metadata!.Id))
                {
                    dataContext.NodeDatas.Add(Metadata!.Id, new EfNodeData
                    {
                        NodeId = Metadata.Id,
                        SingleResult = Metadata.EfNodeSetting!.SingleResult,
                        FormId = dataContext.FormId,
                        ActionDatas = new List<ActionFormData>() { new ActionFormData { State = DataState.Unchanged, FormData = GetFormData(dataContext.DataId)! } }
                    });
                }
                else if (string.IsNullOrEmpty(dataContext.DataId) && dataContext.TriggerData != null && !dataContext.NodeDatas.ContainsKey(Metadata!.Id))
                {
                    dataContext.NodeDatas.Add(Metadata!.Id, new EfNodeData
                    {
                        NodeId = Metadata.Id,
                        SingleResult = Metadata.EfNodeSetting!.SingleResult,
                        FormId = dataContext.FormId,
                        ActionDatas = new List<ActionFormData>() { new ActionFormData { State = DataState.Unchanged, FormData = dataContext.TriggerData } }
                    });
                }

                return ExecutionResult.Next();
            });
        }
    }
}
