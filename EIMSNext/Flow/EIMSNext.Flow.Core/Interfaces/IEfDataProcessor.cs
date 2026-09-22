using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IEfDataProcessor
    {
        bool TryRestoreNode(WorkflowInstance inst, string nodeId, out EfNodeData? nodeData);

        /// <summary>
        /// 执行 EventFlow 节点的写数据动作。
        /// <para>
        /// PostgreSQL 迁移后本方法改为异步：原实现依赖 <c>TransactionScope.ExecuteWithRetry</c>（同步版本），
        /// EF Core 侧只提供 <c>TransactionScope.ExecuteWithRetryAsync</c>。
        /// </para>
        /// </summary>
        Task<EfNodeData> ProcessNodeAsync(WorkflowInstance inst, EfNodeData nodeData, string actionType);

        void Process(WorkflowInstance inst);
    }
}
