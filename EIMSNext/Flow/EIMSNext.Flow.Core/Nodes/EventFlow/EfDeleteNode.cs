using System.Text.Json;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EfDeleteNode : EfNodeBase<EfDeleteNode>
    {
        public EfDeleteNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLog(context, dataContext =>
            {
            var updateSetting = Metadata!.EfNodeSetting!.DeleteSetting!;
            var formDef = GetFormDef(dataContext, updateSetting.FormId);

            List<ActionFormData>? toRemoves = null;
            if (updateSetting.DeleteMode == UpdateMode.Node)
            {
                toRemoves = dataContext.NodeDatas.TryGetValue(updateSetting.NodeId ?? string.Empty, out var nodeData)
                    ? nodeData.ActionDatas
                        .Select(x => new ActionFormData { State = DataState.Removed, FormData = x.FormData })
                        .ToList()
                    : new List<ActionFormData>();
            }
            else if (updateSetting.DeleteMode == UpdateMode.Form)
            {
                var findOpt = updateSetting.DynamicFindOptions!.DeserializeFromJson<DynamicFindOptions<FormData>>()!;
                BuildDynamicFilter(findOpt.Filter!, GetNodeScriptData(dataContext));

                toRemoves = new List<ActionFormData> { };
                FormDataRepository.Find(findOpt).ToList().ForEach(x => toRemoves.Add(new ActionFormData { State = DataState.Removed, FormData = x }));
            }

            if (toRemoves?.Count > 0)
            {
                dataContext.NodeDatas.Add(Metadata!.Id, new EfNodeData
                {
                    NodeId = Metadata.Id,
                    SingleResult = Metadata.EfNodeSetting!.SingleResult,
                    FormId = updateSetting.FormId,
                    ActionDatas = toRemoves
                });
            }

            return ExecutionResult.Next();
            });
        }
    }
}
