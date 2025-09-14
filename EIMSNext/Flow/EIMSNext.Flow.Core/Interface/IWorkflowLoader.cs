using EIMSNext.Entity;

namespace EIMSNext.Flow.Core.Interface
{
    public interface IWorkflowLoader
    {
        void LoadDefinitionsFromStorage();
        void LoadDefinition(Wf_Definition source);
    }
}
