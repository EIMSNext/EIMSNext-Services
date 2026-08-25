using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EfInsertNode : EfNodeBase<EfInsertNode>
    {
        public EfInsertNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLog(context, dataContext =>
            {
                var insertSetting = Metadata!.EfNodeSetting!.InsertSetting!;
                var formDef = GetFormDef(dataContext, insertSetting.FormId);

                if (insertSetting.FieldSettings.Count > 0)
                {
                    //填充字段
                    var insertDatas = BuildInsertDatas(dataContext, formDef, insertSetting.FieldSettings);
                    dataContext.NodeDatas.Add(Metadata!.Id, new EfNodeData
                    {
                        NodeId = Metadata.Id,
                        SingleResult = Metadata.EfNodeSetting!.SingleResult,
                        FormId = insertSetting.FormId,
                        ActionDatas = insertDatas
                    });

                }

                return ExecutionResult.Next();
            });
        }
    }
}
