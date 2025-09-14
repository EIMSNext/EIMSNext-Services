using System.Text.Json;

using HKH.Mef2.Integration;

using EIMSNext.Core.Query;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;
using EIMSNext.Common.Extension;

namespace EIMSNext.Flow.Core.Node
{
    public class DfDeleteNode : DfNodeBase<DfDeleteNode>
    {
        public DfDeleteNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);
            var updateSetting = Metadata!.DfNodeSetting!.DeleteSetting!;
            var formDef = GetFormDef(dataContext, updateSetting.FormId);

            List<ActionFormData>? toRemoves = null;
            if (updateSetting.DeleteMode == UpdateMode.Node)
            {
                toRemoves = dataContext.NodeDatas.FirstOrDefault(x => x.Key == updateSetting.NodeId).Value.ActionDatas.ToList();
            }
            else if (updateSetting.DeleteMode == UpdateMode.Form)
            {
                var findOpt = Metadata!.DfNodeSetting!.UpdateSetting!.DynamicFindOptions!.DeserializeFromJson<DynamicFindOptions<FormData>>()!;
                BuildDynamicFilter(findOpt.Filter!, GetNodeScriptData(dataContext));

                toRemoves = new List<ActionFormData> { };
                FormDataRepository.Find(findOpt).ToList().ForEach(x => toRemoves.Add(new ActionFormData { State = DataState.Removed, FormData = x }));
            }

            if (toRemoves?.Count > 0)
            {
                dataContext.NodeDatas.Add(Metadata!.Id, new DfNodeData
                {
                    NodeId = Metadata.Id,
                    SingleResult = Metadata.DfNodeSetting!.SingleResult,
                    FormId = updateSetting.FormId,
                    ActionDatas = toRemoves
                });
            }

            CreateExecLog(context.Workflow, dataContext, Metadata!);

            return ExecutionResult.Next();
        }
    }
}
