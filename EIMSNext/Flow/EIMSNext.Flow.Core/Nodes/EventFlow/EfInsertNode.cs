using HKH.Mef2.Integration;
using EIMSNext.Flow.Core.Interfaces;

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
                var processor = Resolver.Resolve<IEfDataProcessor>();
                if (processor.TryRestoreNode(context.Workflow, Metadata!.Id, out var restored))
                {
                    dataContext.NodeDatas[Metadata.Id] = restored!;
                    return ExecutionResult.Next();
                }

                var insertSetting = Metadata!.EfNodeSetting!.InsertSetting!;
                var formDef = GetFormDef(dataContext, insertSetting.FormId);

                if (insertSetting.FieldSettings.Count > 0)
                {
                    //填充字段
                    var insertDatas = BuildInsertDatas(dataContext, formDef, insertSetting.FieldSettings);
                    var nodeData = new EfNodeData
                    {
                        NodeId = Metadata.Id,
                        SingleResult = Metadata.EfNodeSetting!.SingleResult,
                        FormId = insertSetting.FormId,
                        ActionDatas = insertDatas
                    };
                    dataContext.NodeDatas[Metadata.Id] = processor.ProcessNode(context.Workflow, nodeData, "insert");

                }

                return ExecutionResult.Next();
            });
        }
    }
}
