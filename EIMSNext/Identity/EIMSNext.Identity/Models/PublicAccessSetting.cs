namespace EIMSNext.Identity.Models
{
    /// <summary>
    /// 公开访问设置。
    /// <para>
    /// 迁移说明：本类不再是 Mongo 的根文档，原 <c>[BsonId]</c> /
    /// <c>[BsonRepresentation(BsonType.String)]</c> 两个特性已移除——<see cref="Id"/> 的
    /// <c>string</c> 契约由 EF Core 的 <c>text</c> 主键直接承载，无需再声明序列化器。
    /// </para>
    /// </summary>
    public sealed class PublicAccessSetting
    {
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
