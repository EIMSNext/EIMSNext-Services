using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interface
{
    public interface IDfDataProcessor
    {
        void Process(WorkflowInstance inst);
    }
}
