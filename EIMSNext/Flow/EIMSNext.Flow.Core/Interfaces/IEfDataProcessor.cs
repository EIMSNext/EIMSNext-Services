using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IEfDataProcessor
    {
        bool TryRestoreNode(WorkflowInstance inst, string nodeId, out EfNodeData? nodeData);
        EfNodeData ProcessNode(WorkflowInstance inst, EfNodeData nodeData, string actionType);
        void Process(WorkflowInstance inst);
    }
}
