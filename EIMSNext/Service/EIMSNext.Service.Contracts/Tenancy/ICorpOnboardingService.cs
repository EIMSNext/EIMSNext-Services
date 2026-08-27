using EIMSNext.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface ICorpOnboardingService
    {
        Task ApplyJoinCorporateAsync(string corpId, User user);
    }
}
