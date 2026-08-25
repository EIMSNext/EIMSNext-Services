using System.Linq;
using System.Text.Json;

using HKH.Mef2.Integration;

using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;

using EIMSNext.Service.Entities;

using MongoDB.Bson.IO;
using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;
using EIMSNext.Common.Extensions;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EfQueryManyNode : EfNodeBase<EfQueryManyNode>
    {
        public EfQueryManyNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLog(context, dataContext =>
            {
                var querySetting = Metadata!.EfNodeSetting!.QueryManySetting!;
                var findOpt = querySetting.DynamicFindOptions!.DeserializeFromJson<DynamicFindOptions<FormData>>()!;
                BuildDynamicFilter(findOpt.Filter!, GetNodeScriptData(dataContext));

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

                return ExecutionResult.Next();
            });
        }
    }
}
