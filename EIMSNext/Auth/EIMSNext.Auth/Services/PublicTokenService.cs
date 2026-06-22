using EIMSNext.ApiService;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Auth.Services
{
    public sealed class PublicTokenService : IPublicTokenService
    {
        private const string UsernamePrefix = "public_";

        private readonly IMongoCollection<PublicAccessSetting> _publicSettings;
        private readonly IMongoCollection<FormRecord> _forms;
        private readonly IMongoCollection<DashboardRecord> _dashboards;
        private readonly string _secretKey;

        public PublicTokenService(IOptions<MongoDbConfiguration> settings, IOptions<PublicAccessOptions> accessOptions)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            var database = client.GetDatabase(settings.Value.Database);
            _publicSettings = database.GetCollection<PublicAccessSetting>("PublicSetting");
            _forms = database.GetCollection<FormRecord>("FormDef");
            _dashboards = database.GetCollection<DashboardRecord>("DashboardDef");
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

            var setting = _publicSettings.AsQueryable()
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

            var name = ResolveName(setting);
            return new PublicTokenSubject(targetId, setting.CorpId, setting.AppId, name);
        }

        private string ResolveName(PublicAccessSetting setting)
        {
            if (setting.TargetType == 1)
            {
                return _dashboards.AsQueryable()
                    .Where(x => x.Id == setting.TargetId && !x.DeleteFlag)
                    .Select(x => x.Name)
                    .FirstOrDefault() ?? "public";
            }

            return _forms.AsQueryable()
                .Where(x => x.Id == setting.TargetId && !x.DeleteFlag)
                .Select(x => x.Name)
                .FirstOrDefault() ?? "public";
        }

        private sealed class FormRecord
        {
            [MongoDB.Bson.Serialization.Attributes.BsonId]
            [MongoDB.Bson.Serialization.Attributes.BsonRepresentation(BsonType.String)]
            public string Id { get; set; } = string.Empty;
            public bool DeleteFlag { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private sealed class DashboardRecord
        {
            [MongoDB.Bson.Serialization.Attributes.BsonId]
            [MongoDB.Bson.Serialization.Attributes.BsonRepresentation(BsonType.String)]
            public string Id { get; set; } = string.Empty;
            public bool DeleteFlag { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
