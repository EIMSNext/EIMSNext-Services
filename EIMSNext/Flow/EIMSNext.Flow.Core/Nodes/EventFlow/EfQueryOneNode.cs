using System.Text.Json;

using HKH.Mef2.Integration;

using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Entities;

using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;
using EIMSNext.Common.Extensions;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EfQueryOneNode : EfNodeBase<EfQueryOneNode>
    {
        public EfQueryOneNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLog(context, dataContext =>
            {
                var querySetting = Metadata!.EfNodeSetting!.QueryOneSetting!;
                var findOpt = querySetting.DynamicFindOptions!.DeserializeFromJson<DynamicFindOptions<FormData>>()!;
                BuildDynamicFilter(findOpt.Filter!, GetNodeScriptData(dataContext));

                var queryData = FormDataRepository.Find(findOpt).FirstOrDefault();

                if (queryData != null)
                {
                    dataContext.NodeDatas.Add(Metadata!.Id, new EfNodeData
                    {
                        NodeId = Metadata.Id,
                        SingleResult = Metadata.EfNodeSetting!.SingleResult,
                        FormId = querySetting.FormId,
                        ActionDatas = new List<ActionFormData>() { new ActionFormData { State = DataState.Unchanged, FormData = queryData } }
                    });
                }

                return ExecutionResult.Next();
            });
        }
    }
}
