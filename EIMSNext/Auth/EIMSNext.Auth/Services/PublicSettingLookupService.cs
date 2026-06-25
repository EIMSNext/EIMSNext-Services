using EIMSNext.ApiService;
using EIMSNext.Auth.Models;
using MongoDB.Driver;

namespace EIMSNext.Auth.Services
{
    public sealed class PublicSettingLookupService
    {
        private readonly IMongoCollection<PublicAccessSetting> _publicSettings;

        public PublicSettingLookupService(IMongoCollection<PublicAccessSetting> publicSettings)
        {
            _publicSettings = publicSettings;
        }

        public bool IsAnySectionEnabled(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return false;

            var setting = _publicSettings.AsQueryable()
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
