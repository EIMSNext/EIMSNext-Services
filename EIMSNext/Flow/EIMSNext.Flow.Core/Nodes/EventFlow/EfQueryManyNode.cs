using System.Linq;
using System.Text.Json;

using HKH.Mef2.Integration;

using EIMSNext.Core.Query;

using EIMSNext.Entities;


using WorkflowCore.Interface;
using WorkflowCore.Models;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Repositories;
using EIMSNext.Core;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EfQueryManyNode : EfNodeBase<EfQueryManyNode>
    {
        public EfQueryManyNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLogAsync(context, dataContext =>
            {
                var querySetting = Metadata!.EfNodeSetting!.QueryManySetting!;
                var findOpt = querySetting.DynamicFindOptions!.DeserializeFromJson<DynamicFindOptions<FormData>>()!;
                BuildDynamicFilter(findOpt.Filter!, GetNodeScriptData(dataContext));
                findOpt.Take = Math.Min(
                    findOpt.GetEffectiveTake() <= 0 ? EIMSNext.Component.WfMetadataParser.DefaultEventFlowNodeTake : findOpt.Take,
                    EIMSNext.Component.WfMetadataParser.DefaultEventFlowNodeTake);

                var queryData = FormDataRepository.Find(findOpt).ToList();

                if (queryData?.Count > 0)
                {
                    var datas = new List<ActionFormData>();
                    queryData.ForEach(x => datas.Add(new ActionFormData { State = DataState.Unchanged, FormData = x }));
                    dataContext.NodeDatas.Add(Metadata!.Id, new EfNodeData
                    {
                        NodeId = Metadata.Id,
                        SingleResult = Metadata.EfNodeSetting!.SingleResult,
                        FormId = querySetting.FormId,
                        ActionDatas = datas
                    });
                }

                return Task.FromResult(ExecutionResult.Next());
            }).GetAwaiter().GetResult();
        }
    }
}
