using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IFormNotifyRecipientResolver
    {
        Task<List<NotifyReceiver>> ResolveAsync(FormData data, FormDef formDef, string? notifiersJson, string? operatorEmpId);
    }
}
