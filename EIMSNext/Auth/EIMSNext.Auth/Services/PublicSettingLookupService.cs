using EIMSNext.ApiService;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Auth.Services
{
    public sealed class PublicSettingLookupService
    {
        private readonly IMongoCollection<PublicSettingRecord> _publicSettings;

        public PublicSettingLookupService(IOptions<MongoDbConfiguration> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            var database = client.GetDatabase(settings.Value.Database);
            _publicSettings = database.GetCollection<PublicSettingRecord>("PublicSetting");
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

        private static bool SectionEnabled(PublishSectionRecord? s)
        {
            if (s == null || !s.Enabled) return false;
            if (s.ExpireTime.HasValue && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > s.ExpireTime.Value)
                return false;
            return true;
        }

        private sealed class PublicSettingRecord
        {
            [MongoDB.Bson.Serialization.Attributes.BsonId]
            [MongoDB.Bson.Serialization.Attributes.BsonRepresentation(BsonType.String)]
            public string Id { get; set; } = string.Empty;
            public bool DeleteFlag { get; set; }
            public int TargetType { get; set; }
            public string TargetId { get; set; } = string.Empty;
            public PublishSectionRecord? Dashboard { get; set; }
            public PublicFormSettingRecord? Form { get; set; }
        }

        private sealed class PublicFormSettingRecord
        {
            public PublishSectionRecord? FormLink { get; set; }
            public PublishSectionRecord? DataLink { get; set; }
            public PublishSectionRecord? QueryLink { get; set; }
        }

        private sealed class PublishSectionRecord
        {
            public bool Enabled { get; set; }
            public long? ExpireTime { get; set; }
            public bool AccessCodeEnabled { get; set; }
            public string AccessCodeHash { get; set; } = string.Empty;
        }
    }
}
