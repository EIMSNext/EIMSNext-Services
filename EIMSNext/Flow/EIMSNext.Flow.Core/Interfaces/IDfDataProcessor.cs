using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IDfDataProcessor
    {
        void Process(WorkflowInstance inst);
    }
}
