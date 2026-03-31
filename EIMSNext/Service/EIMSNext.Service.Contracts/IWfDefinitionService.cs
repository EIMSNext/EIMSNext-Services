using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
	public interface IWfDefinitionService : IService<Wf_Definition>
	{
        Wf_Definition? Find(string wfExternalId, int? version = null);
    }
}
