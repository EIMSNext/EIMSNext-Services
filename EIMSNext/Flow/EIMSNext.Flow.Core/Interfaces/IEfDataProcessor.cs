using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IEfDataProcessor
    {
        bool TryRestoreNode(WorkflowInstance inst, string nodeId, out EfNodeData? nodeData);

        /// <summary>
        /// 执行 EventFlow 节点的写数据动作。
        /// <para>
        /// </para>
        /// </summary>
        Task<EfNodeData> ProcessNodeAsync(WorkflowInstance inst, EfNodeData nodeData, string actionType);

        void Process(WorkflowInstance inst);
    }
}
