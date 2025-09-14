using EIMSNext.Core.Service;
using EIMSNext.Entity;

namespace EIMSNext.Service.Interface
{
	public interface IWfDefinitionService : IService<Wf_Definition>
	{
        Wf_Definition? Find(string wfExternalId, int? version = null);
    }
}
