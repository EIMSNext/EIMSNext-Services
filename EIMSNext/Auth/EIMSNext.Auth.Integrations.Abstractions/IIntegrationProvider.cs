using EIMSNext.Auth.Entities;

namespace EIMSNext.Auth.Integrations.Abstractions
{
    public interface IIntegrationProvider
    {
        string Type { get; }

        IntegrationProviderCapability Capability { get; }

        Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default);

        string BuildAuthorizationUrl(IntegrationLoginSetting setting, string state);
    }
}
