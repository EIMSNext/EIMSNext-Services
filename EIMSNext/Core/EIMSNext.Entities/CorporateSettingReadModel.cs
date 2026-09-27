using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

namespace EIMSNext.Entities;

/// <summary>
/// Read-only projection of the business CorporateSetting collection.
/// </summary>
public sealed class CorporateSettingReadModel : KeyedEntityBase
{
    public string? CorpId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Desc { get; set; } = string.Empty;

    public bool DeleteFlag { get; set; }
}
