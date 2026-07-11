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

        public PublicTokenSubject? Validate(string? username, string? password, PublicScope scope)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                !username.StartsWith(UsernamePrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var targetId = username[UsernamePrefix.Length..];
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return null;
            }

            var setting = _dbContext.PublicSettings
                .Where(x => !x.DeleteFlag && x.TargetId == targetId)
                .ToList()
                .FirstOrDefault();

            if (setting == null || string.IsNullOrWhiteSpace(setting.CorpId))
            {
                return null;
            }

            var section = PublicAccessValidator.ResolveSection(setting, scope);
            if (!PublicAccessValidator.ValidateSection(section, password, setting.TargetId, _secretKey))
            {
                return null;
            }

            return new PublicTokenSubject(targetId, setting.CorpId, setting.AppId);
        }
    }
}
