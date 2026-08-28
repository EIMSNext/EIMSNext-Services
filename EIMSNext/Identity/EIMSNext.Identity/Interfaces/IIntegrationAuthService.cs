using EIMSNext.Identity.Abstractions;
using EIMSNext.Identity.Models;

namespace EIMSNext.Identity.Interfaces
{
    public interface IIntegrationAuthService
    {
        Task<IntegrationValidationResult> ValidateAsync(string? integrationType, string? password, CancellationToken cancellationToken = default);

        Task<IntegrationAuthorizationUrlResult> GetAuthorizationUrlAsync(string integrationType, string state, CancellationToken cancellationToken = default);
    }
}
