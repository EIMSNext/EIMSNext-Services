using System.Text.Json;

using HKH.Mef2.Integration;

using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;

using MongoDB.Driver;

using WorkflowCore.Interface;
using WorkflowCore.Models;
using EIMSNext.Common.Extensions;

namespace EIMSNext.Flow.Core.Nodes
{
    public class DfQueryOneNode : DfNodeBase<DfQueryOneNode>
    {
        public DfQueryOneNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLog(context, dataContext =>
            {
                var findOpt = Metadata!.DfNodeSetting!.QueryOneSetting!.DynamicFindOptions!.DeserializeFromJson<DynamicFindOptions<FormData>>()!;
                BuildDynamicFilter(findOpt.Filter!, GetNodeScriptData(dataContext));

                var queryData = FormDataRepository.Find(findOpt).FirstOrDefault();

                if (queryData != null)
                {
                    dataContext.NodeDatas.Add(Metadata!.Id, new DfNodeData
                    {
                        NodeId = Metadata.Id,
                        SingleResult = Metadata.DfNodeSetting!.SingleResult,
                        FormId = dataContext.FormId,
                        ActionDatas = new List<ActionFormData>() { new ActionFormData { State = DataState.Unchanged, FormData = queryData } }
                    });
                }

                return ExecutionResult.Next();
            });
        }
    }
}
