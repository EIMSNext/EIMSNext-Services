using System.Dynamic;

using EIMSNext.Service.Entities;

using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;

namespace EIMSNext.Flow.Core.Nodes
{
    /// <summary>
    /// 条件/分支节点。继承自 WorkflowCore 的 <see cref="Decide"/>，行为差异如下：
    /// <list type="bullet">
    ///   <item>
    ///     重置 <see cref="DfDataContext.MatchedResult"/> 与 <see cref="DfDataContext.MatchParallel"/>，
    ///     供下游 <c>WfStartNode</c> 在 SelectNextStep 时读取。
    ///   </item>
    ///   <item>
    ///     本节点 <b>不</b>计算任何条件表达式——直接 <see cref="ExecutionResult.Outcome(string)"/>
    ///     返回的 <see cref="Decide.Expression"/> 是 WorkflowCore 默认分支（通常是 <c>Outcome("True")</c>），
    ///     与本系统的 <c>SelectNextStep</c> 映射无关。
    ///   </item>
    ///   <item>
    ///     真正的分支选择由 <c>WorkflowLoader.AttachOutcomes</c> 注入：通过
    ///     <see cref="ExpressionEvaluator.EvaluateOutcomeExpression"/> 解析每个
    ///     <c>SelectNextStep</c> 项的 JS 表达式，把命中的 <c>OutcomeValue</c> 写回
    ///     <c>context.Workflow.SubsequentSteps</c>。
    ///   </item>
    /// </list>
    /// </summary>
    public class WfDecideNode : Decide, IFlowNode
    {
        public WfStep? Metadata { get; set; }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            if (context.Workflow.Data is DfDataContext dfDataContext)
            {
                dfDataContext.MatchedResult = false;
                dfDataContext.MatchParallel = Metadata!.NodeType== WfNodeType.Branch;
            }
            else
            {
                var wfDataContext = (ExpandoObject)context.Workflow.Data;
                wfDataContext.AddOrUpdate(WfConsts.MatchedResult, false);
                wfDataContext.AddOrUpdate(WfConsts.MatchParallel, Metadata!.NodeType == WfNodeType.Branch);
            }

            return ExecutionResult.Outcome(Expression);
        }
    }
}
