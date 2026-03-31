using WorkflowCore.Interface;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IExpressionEvaluator
    {
        object? EvaluateExpression(string sourceExpr, object pData, IStepExecutionContext pContext);
        bool EvaluateOutcomeExpression(string sourceExpr, object data, object outcome);
    }
}