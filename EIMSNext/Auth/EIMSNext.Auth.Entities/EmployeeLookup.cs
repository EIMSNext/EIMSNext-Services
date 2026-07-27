using MongoDB.Bson.Serialization.Attributes;

namespace EIMSNext.Auth.Entities;

/// <summary>Minimal projection of the shared Employee collection for SSO lookup.</summary>
public sealed class EmployeeLookup
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("corpId")]
    public string CorpId { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("status")]
    public int Status { get; set; }
}
