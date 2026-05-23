using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Models;

namespace EIMSNext.Auth.Interfaces
{
    public interface IIntegrationProvider
    {
        string Type { get; }

        Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default);

        string BuildAuthorizationUrl(IntegrationLoginSetting setting, string state);
    }
}
