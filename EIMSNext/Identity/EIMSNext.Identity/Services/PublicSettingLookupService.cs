using EIMSNext.ApiService;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;

namespace EIMSNext.Identity.Services
{
    public sealed class PublicSettingLookupService
    {
        private readonly IIdentityDbContext _dbContext;

        public PublicSettingLookupService(IIdentityDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public bool IsAnySectionEnabled(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return false;

            var setting = _dbContext.PublicSettings
                .Where(x => !x.DeleteFlag && x.TargetId == targetId)
                .ToList()
                .FirstOrDefault();
            if (setting == null) return false;

            if (setting.TargetType == 1)
            {
                return SectionEnabled(setting.Dashboard);
            }

            return SectionEnabled(setting.Form?.FormLink)
                || SectionEnabled(setting.Form?.DataLink)
                || SectionEnabled(setting.Form?.QueryLink);
        }

        private static bool SectionEnabled(PublishSection? s)
        {
            if (s == null || !s.Enabled) return false;
            if (s.ExpireTime.HasValue && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > s.ExpireTime.Value)
                return false;
            return true;
        }
    }
}
