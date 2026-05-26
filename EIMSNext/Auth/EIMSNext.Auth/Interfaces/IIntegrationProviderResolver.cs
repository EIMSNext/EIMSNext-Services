using EIMSNext.Auth.Integrations.Abstractions;

namespace EIMSNext.Auth.Interfaces
{
    public interface IIntegrationProviderResolver
    {
        bool TryGetById(string id, out IIntegrationProvider? provider);
    }
}
