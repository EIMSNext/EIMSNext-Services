using System.Linq;
using System.Text.Json;

using HKH.Mef2.Integration;

using EIMSNext.Core.Query;

using EIMSNext.Entity;

using MongoDB.Bson.IO;
using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;
using EIMSNext.Common.Extension;

namespace EIMSNext.Flow.Core.Node
{
    public class DfQueryManyNode : DfNodeBase<DfQueryManyNode>
    {
        public DfQueryManyNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);

            var findOpt = Metadata!.DfNodeSetting!.QueryManySetting!.DynamicFindOptions!.DeserializeFromJson<DynamicFindOptions<FormData>>()!;
            BuildDynamicFilter(findOpt.Filter!, GetNodeScriptData(dataContext));

            var queryData = FormDataRepository.Find(findOpt).ToList();

            if (queryData?.Count > 0)
            {
                var datas = new List<ActionFormData>();
                queryData.ForEach(x => datas.Add(new ActionFormData { State = DataState.Unchanged, FormData = x }));
                dataContext.NodeDatas.Add(Metadata!.Id, new DfNodeData
                {
                    NodeId = Metadata.Id,
                    SingleResult = Metadata.DfNodeSetting!.SingleResult,
                    FormId = dataContext.FormId,
                    ActionDatas = datas
                });
            }

            CreateExecLog(context.Workflow, dataContext, Metadata!);

            return ExecutionResult.Next();
        }
    }
}
