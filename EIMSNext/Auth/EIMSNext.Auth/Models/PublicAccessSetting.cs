using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EIMSNext.Auth.Models
{
    public sealed class PublicAccessSetting
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;

        public string? CorpId { get; set; }
        public bool DeleteFlag { get; set; }
        public string AppId { get; set; } = string.Empty;
        public int TargetType { get; set; }
        public string TargetId { get; set; } = string.Empty;
        public PublicFormAccessSetting? Form { get; set; }
        public PublishSection? Dashboard { get; set; }
    }

    public sealed class PublicFormAccessSetting
    {
        public PublishSection? FormLink { get; set; }
        public PublishSection? DataLink { get; set; }
        public PublishSection? QueryLink { get; set; }
    }

    public sealed class PublishSection
    {
        public bool Enabled { get; set; }
        public long? ExpireTime { get; set; }
        public bool AccessCodeEnabled { get; set; }
        public string AccessCodeHash { get; set; } = string.Empty;
    }
}
