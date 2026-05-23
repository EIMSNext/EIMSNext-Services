using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using HKH.Common.Security;

namespace EIMSNext.Auth.Services
{
    public sealed class IntegrationAuthService : IIntegrationAuthService
    {
        private readonly IAuthDbContext _dbContext;
        private readonly IReadOnlyDictionary<string, IIntegrationProvider> _providers;

        public IntegrationAuthService(IAuthDbContext dbContext, IEnumerable<IIntegrationProvider> providers)
        {
            _dbContext = dbContext;
            _providers = providers.ToDictionary(x => x.Type, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<User?> ValidateAsync(string? integrationType, string? password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(integrationType) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var setting = FindEnabledIntegrationSetting(integrationType);
            if (setting == null || !_providers.TryGetValue(setting.Type, out var provider))
            {
                return null;
            }

            var payload = IntegrationAuthPayload.Parse(password);
            if (string.IsNullOrWhiteSpace(payload.Code))
            {
                return null;
            }

            var authResult = await provider.AuthenticateAsync(setting, payload, cancellationToken);
            var binding = FindBinding(setting.Type, authResult);
            if (binding == null)
            {
                if (!string.Equals(setting.Type, IntegrationLoginType.WeChat, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return await CreateWeChatUserAsync(authResult);
            }

            return FindActiveUser(binding.UserId);
        }

        public Task<IntegrationAuthorizationUrlResult> GetAuthorizationUrlAsync(string integrationType, string state, CancellationToken cancellationToken = default)
        {
            var setting = FindEnabledIntegrationSetting(integrationType);
            if (setting == null || !setting.Enabled || !_providers.TryGetValue(setting.Type, out var provider))
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

        private async Task<User> CreateWeChatUserAsync(IntegrationAuthResult authResult)
        {
            var user = new User
            {
                Name = string.IsNullOrWhiteSpace(authResult.DisplayName) ? "微信用户" : authResult.DisplayName,
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
                IntegrationType = IntegrationLoginType.WeChat,
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
    }
}
