using EIMSNext.Common.Extensions;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    /// <summary>
    /// EventFlow 打印节点。
    /// </summary>
    public class EfPrintNode : EfNodeBase<EfPrintNode>
    {
        public EfPrintNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLogAsync(context, dataContext =>
            {
                var setting = Metadata?.EfNodeSetting?.PrintSetting
                    ?? throw new InvalidOperationException("打印节点未配置");

                if (string.IsNullOrWhiteSpace(setting.SourceNodeId)
                    || string.IsNullOrWhiteSpace(setting.FormId)
                    || string.IsNullOrWhiteSpace(setting.PrintDefId))
                {
                    throw new InvalidOperationException("打印节点配置不完整");
                }

                var pending = new EfPrintRenderer(Resolver)
                    .Render(dataContext, setting.SourceNodeId, setting.FormId, setting.PrintDefId);

                //产物先挂 run 上下文，下游首次消费 printFile 时才上传（MaterializePrintOutput），未消费即随 run 丢弃；
                //marker 随输出持久化，携带重渲染要素：run 跨进程恢复后产物丢失时由消费点就地重渲染
                dataContext.PendingPrints[Metadata!.Id] = pending;

                var attachmentData = new Dictionary<string, object?>
                {
                    [EfPendingPrintFile.MarkerKey] = Metadata.Id,
                    ["fileName"] = pending.FileName,
                    [EfPendingPrintFile.PrintDefIdKey] = setting.PrintDefId,
                    [EfPendingPrintFile.FormIdKey] = setting.FormId,
                    [EfPendingPrintFile.SourceNodeIdKey] = setting.SourceNodeId,
                };

                var outputData = new Dictionary<string, object?>();
                outputData["printFile"] = attachmentData;
                var output = new FormData
                {
                    AppId = dataContext.AppId,
                    CorpId = dataContext.CorpId,
                    FormId = string.Empty,
                    Data = outputData,
                    CreateBy = dataContext.WfStarter,
                    CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                };

                dataContext.NodeDatas[Metadata!.Id] = new EfNodeData
                {
                    NodeId = Metadata.Id,
                    SingleResult = true,
                    FormId = setting.FormId,
                    NodeExecResult = attachmentData,
                    ActionDatas = [new ActionFormData { State = DataState.Unchanged, FormData = output }],
                };

                return Task.FromResult(ExecutionResult.Next());
            }, "打印成功").GetAwaiter().GetResult();
        }
    }
}
