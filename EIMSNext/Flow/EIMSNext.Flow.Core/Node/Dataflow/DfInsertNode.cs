using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public class DfInsertNode : DfNodeBase<DfInsertNode>
    {
        public DfInsertNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);
            var insertSetting = Metadata!.DfNodeSetting!.InsertSetting!;
            var formDef = GetFormDef(dataContext, insertSetting.FormId);

            if (insertSetting.FieldSettings.Count > 0)
            {
                //填充字段
                var insertDatas = BuildInsertDatas(dataContext, formDef, insertSetting.FieldSettings);
                dataContext.NodeDatas.Add(Metadata!.Id, new DfNodeData
                {
                    NodeId = Metadata.Id,
                    SingleResult = Metadata.DfNodeSetting!.SingleResult,
                    FormId = insertSetting.FormId,
                    ActionDatas = insertDatas
                });

            }

            CreateExecLog(context.Workflow, dataContext, Metadata!);

            return ExecutionResult.Next();
        }
    }
}
