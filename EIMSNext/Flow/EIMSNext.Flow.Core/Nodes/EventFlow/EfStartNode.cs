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
            return ExecuteWithLogAsync(context, dataContext =>
            {
                if (!string.IsNullOrEmpty(dataContext.DataId) && !dataContext.NodeDatas.ContainsKey(Metadata!.Id))
                {
                    // 删除事件在业务侧软删除之后才触发，按 Id 查询走全局过滤会取不到；
                    // 此时回退到触发时携带的实体，保证 Removed 事件流能拿到数据真正执行。
                    var formData = GetFormData(dataContext.DataId) ?? dataContext.TriggerData;
                    if (formData != null)
                    {
                        dataContext.NodeDatas.Add(Metadata!.Id, new EfNodeData
                        {
                            NodeId = Metadata.Id,
                            SingleResult = Metadata.EfNodeSetting!.SingleResult,
                            FormId = dataContext.FormId,
                            ActionDatas = new List<ActionFormData>() { new ActionFormData { State = DataState.Unchanged, FormData = formData } }
                        });
                    }
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

                return Task.FromResult(ExecutionResult.Next());
            }).GetAwaiter().GetResult();
        }
    }
}
