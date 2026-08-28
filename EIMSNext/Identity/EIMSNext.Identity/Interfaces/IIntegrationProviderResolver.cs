using EIMSNext.Identity.Abstractions;

namespace EIMSNext.Identity.Interfaces
{
    public interface IIntegrationProviderResolver
    {
        bool TryGetById(string id, out IIntegrationProvider? provider);
    }
}
