using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IFormNotifyDetailBuilder
    {
        string BuildForAdd(FormData data, FormDef formDef);
        string BuildForChange(FormData oldData, FormData newData, FormDef formDef);
    }
}
