using EIMSNext.Service.Entities;

namespace EIMSNext.Flow.Core.Interfaces
{
    public interface IWorkflowLoader
    {
        void LoadDefinitionsFromStorage();
        void LoadDefinition(Wf_Definition source);
    }
}
