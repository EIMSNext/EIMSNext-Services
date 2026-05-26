using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Integrations.Abstractions;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using HKH.Common.Security;

namespace EIMSNext.Auth.Services
{
    public sealed class IntegrationAuthService : IIntegrationAuthService
    {
        private readonly IAuthDbContext _dbContext;
        private readonly IIntegrationProviderResolver _providerResolver;

        public IntegrationAuthService(IAuthDbContext dbContext, IIntegrationProviderResolver providerResolver)
        {
            _dbContext = dbContext;
            _providerResolver = providerResolver;
        }

        public async Task<IntegrationValidationResult> ValidateAsync(string? integrationType, string? password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(integrationType) || string.IsNullOrWhiteSpace(password))
            {
                return Failure();
            }

            var setting = FindEnabledIntegrationSetting(integrationType);
            if (setting == null || !_providerResolver.TryGetById(setting.Type, out var provider) || provider == null)
            {
                return Failure();
            }

            var payload = IntegrationAuthPayload.Parse(password);
            if (string.IsNullOrWhiteSpace(payload.Code))
            {
                return Failure(provider.Capability.UnboundFailureMessage);
            }

            var authResult = await provider.AuthenticateAsync(setting, payload, cancellationToken);
            var binding = FindBinding(setting.Type, authResult);
            if (binding == null)
            {
                if (!provider.Capability.CanAutoProvisionUser)
                {
                    return Failure(provider.Capability.UnboundFailureMessage);
                }

                return new IntegrationValidationResult
                {
                    User = await CreateUserAsync(authResult, provider.Capability)
                };
            }

            var user = FindActiveUser(binding.UserId);
            return user == null
                ? Failure(provider.Capability.UnboundFailureMessage)
                : new IntegrationValidationResult { User = user };
        }

        public Task<IntegrationAuthorizationUrlResult> GetAuthorizationUrlAsync(string integrationType, string state, CancellationToken cancellationToken = default)
        {
            var setting = FindEnabledIntegrationSetting(integrationType);
            if (setting == null || !setting.Enabled || !_providerResolver.TryGetById(setting.Type, out var provider) || provider == null)
            {
                return Task.FromResult(new IntegrationAuthorizationUrlResult
                {
                    Type = integrationType,
                    Enabled = false,
                    DisplayName = setting?.DisplayName ?? integrationType,
                    AuthorizationUrl = string.Empty
                });
            }

            return Task.FromResult(new IntegrationAuthorizationUrlResult
            {
                Type = setting.Type,
                Enabled = true,
                DisplayName = string.IsNullOrWhiteSpace(setting.DisplayName) ? setting.Type : setting.DisplayName,
                AuthorizationUrl = provider.BuildAuthorizationUrl(setting, state)
            });
        }

        private IntegrationLoginSetting? FindEnabledIntegrationSetting(string integrationType)
        {
            return _dbContext.IntegrationLoginSettings.FirstOrDefault(x => x.Type == integrationType && x.Enabled);
        }

        private User? FindActiveUser(string userId)
        {
            return _dbContext.Users.FirstOrDefault(x => x.Id == userId && !x.Disabled);
        }

        private UserIntegrationBinding? FindBinding(string integrationType, IntegrationAuthResult authResult)
        {
            if (!string.IsNullOrWhiteSpace(authResult.UnionId))
            {
                var binding = _dbContext.UserIntegrationBindings.FirstOrDefault(x => x.IntegrationType == integrationType && x.UnionId == authResult.UnionId && x.Enabled);
                if (binding != null)
                {
                    return binding;
                }
            }

            if (!string.IsNullOrWhiteSpace(authResult.OpenId))
            {
                var binding = _dbContext.UserIntegrationBindings.FirstOrDefault(x => x.IntegrationType == integrationType && x.OpenId == authResult.OpenId && x.Enabled);
                if (binding != null)
                {
                    return binding;
                }
            }

            if (!string.IsNullOrWhiteSpace(authResult.ExternalUserId))
            {
                return _dbContext.UserIntegrationBindings.FirstOrDefault(x => x.IntegrationType == integrationType && x.ExternalUserId == authResult.ExternalUserId && x.Enabled);
            }

            return null;
        }

        private async Task<User> CreateUserAsync(IntegrationAuthResult authResult, IntegrationProviderCapability capability)
        {
            var user = new User
            {
                Name = string.IsNullOrWhiteSpace(authResult.DisplayName) ? capability.DefaultUserName : authResult.DisplayName,
                Platform = EIMSNext.Core.Entities.PlatformType.Public,
                Password = BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                Email = string.Empty,
                Phone = string.Empty,
                Crops = new List<UserCorp>()
            };

            await _dbContext.AddUser(user);

            var binding = new UserIntegrationBinding
            {
                UserId = user.Id,
                IntegrationType = authResult.IntegrationType,
                OpenId = authResult.OpenId,
                UnionId = authResult.UnionId,
                ExternalUserId = authResult.ExternalUserId,
                Avatar = authResult.Avatar,
                NickName = authResult.DisplayName,
                CorpId = authResult.CorpId,
                TenantId = authResult.TenantId,
                Enabled = true
            };

            await _dbContext.AddUserIntegrationBinding(binding);
            return user;
        }

        private static IntegrationValidationResult Failure(string message = "")
        {
            return new IntegrationValidationResult
            {
                FailureMessage = message
            };
        }
    }
}
