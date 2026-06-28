using HKH.Mef2.Integration;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    /// <summary>
    /// Dataflow 打印节点（占位/标记）。
    /// <para>
    /// 当前实现仅作为流程占位节点：触发 <see cref="DfNodeBase.ExecuteWithLog"/> 记录运行日志后
    /// 直接 <see cref="ExecutionResult.Next"/>，<b>不会</b>实际调用任何打印服务。
    /// </para>
    /// <para>
    /// 历史原因：早期设计里该节点依赖 <c>IPrintService</c>，但打印服务接口在后续重构中被
    /// 移除而本节点没有同步实现。若业务需要触发打印，请在自定义 dataflow 节点里注入
    /// <c>IPrintService</c> 并替换本节点。
    /// </para>
    /// </summary>
    public class DfPrintNode : DfNodeBase<DfPrintNode>
    {
        public DfPrintNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLog(context, dataContext => ExecutionResult.Next());
        }
    }
}
