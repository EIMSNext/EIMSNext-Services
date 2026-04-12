using EIMSNext.Service.Entities;

using EIMSNext.Async.Abstractions.Messaging;

namespace EIMSNext.Service.Contracts
{
    public interface IFormNotifyRecipientResolver
    {
        Task<List<NotifyReceiver>> ResolveAsync(FormData data, FormDef formDef, string? notifiersJson, string? operatorEmpId);
        Task<List<NotifyReceiver>> ResolveCandidatesAsync(FormData data, FormDef formDef, IEnumerable<ApprovalCandidate> candidates, string? operatorEmpId);
    }
}
