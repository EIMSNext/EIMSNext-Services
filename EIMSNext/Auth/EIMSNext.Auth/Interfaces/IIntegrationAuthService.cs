using EIMSNext.Auth.Integrations.Abstractions;
using EIMSNext.Auth.Models;

namespace EIMSNext.Auth.Interfaces
{
    public interface IIntegrationAuthService
    {
        Task<IntegrationValidationResult> ValidateAsync(string? integrationType, string? password, CancellationToken cancellationToken = default);

        Task<IntegrationAuthorizationUrlResult> GetAuthorizationUrlAsync(string integrationType, string state, CancellationToken cancellationToken = default);
    }
}
