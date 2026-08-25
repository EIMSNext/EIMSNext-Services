using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IEfDataProcessor
    {
        void Process(WorkflowInstance inst);
    }
}
