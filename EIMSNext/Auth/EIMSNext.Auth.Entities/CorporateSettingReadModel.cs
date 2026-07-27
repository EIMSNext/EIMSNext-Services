using EIMSNext.Core.Entities;

namespace EIMSNext.Auth.Entities;

/// <summary>
/// Read-only projection of the business CorporateSetting collection.
/// </summary>
public sealed class CorporateSettingReadModel : MongoEntityBase
{
    public string? CorpId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Desc { get; set; } = string.Empty;

    public bool DeleteFlag { get; set; }
}
