using EIMSNext.ApiService;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using Microsoft.Extensions.Options;

namespace EIMSNext.Auth.Services
{
    public sealed class PublicTokenService : IPublicTokenService
    {
        private const string UsernamePrefix = "public_";

        private readonly IAuthDbContext _dbContext;
        private readonly string _secretKey;

        public PublicTokenService(
            IAuthDbContext dbContext,
            IOptions<PublicAccessOptions> accessOptions)
        {
            _dbContext = dbContext;
            _secretKey = accessOptions.Value.SecretKey;
        }

        public PublicTokenValidationResult Validate(string? username, string? password, PublicScope scope)
        {
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
                .ToList()
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

            if (!PublicAccessValidator.ValidateSection(section, password, setting.TargetId, _secretKey))
            {
                return PublicTokenValidationResult.Invalid("公开访问凭证无效");
            }

            return PublicTokenValidationResult.Success(new PublicTokenSubject(targetId, setting.CorpId, setting.AppId));
        }
    }
}
