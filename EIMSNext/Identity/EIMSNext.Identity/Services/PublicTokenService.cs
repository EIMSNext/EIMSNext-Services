using EIMSNext.ApiService;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;

namespace EIMSNext.Identity.Services
{
    public sealed class PublicTokenService : IPublicTokenService
    {
        private const string UsernamePrefix = "public_";

        private readonly IIdentityDbContext _dbContext;
        private readonly PublicAccessKeyProvider _accessKeyProvider;

        public PublicTokenService(IIdentityDbContext dbContext, PublicAccessKeyProvider accessKeyProvider)
        {
            _dbContext = dbContext;
            _accessKeyProvider = accessKeyProvider;
        }

        public PublicTokenValidationResult Validate(string? username, string? password, PublicScope scope)
        {
            var secretKey = _accessKeyProvider.GetSecretKey();
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                // 密钥未初始化时 fail-closed，防止退化成无签名校验。
                return PublicTokenValidationResult.Invalid("公开访问凭证无效");
            }

            if (string.IsNullOrWhiteSpace(username) ||
                !username.StartsWith(UsernamePrefix, StringComparison.Ordinal))
            {
                return PublicTokenValidationResult.Invalid("公开访问凭证无效");
            }

            var targetId = username[UsernamePrefix.Length..];
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return PublicTokenValidationResult.Invalid("公开访问凭证无效");
            }

            var setting = _dbContext.PublicSettings
                .Where(x => !x.DeleteFlag && x.TargetId == targetId)
                .FirstOrDefault();

            if (setting == null || string.IsNullOrWhiteSpace(setting.CorpId))
            {
                return PublicTokenValidationResult.Invalid("公开访问凭证无效");
            }

            var section = PublicAccessValidator.ResolveSection(setting, scope);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (section?.ExpireTime is long expireTime && now > expireTime)
            {
                return PublicTokenValidationResult.Invalid("公开访问链接已过期");
            }

            if (!PublicAccessValidator.ValidateSection(section, password, setting.TargetId, secretKey))
            {
                return PublicTokenValidationResult.Invalid("公开访问凭证无效");
            }

            return PublicTokenValidationResult.Success(new PublicTokenSubject(targetId, setting.CorpId, setting.AppId));
        }
    }
}
