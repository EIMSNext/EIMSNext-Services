using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Entities;

/// <summary>
/// 企业级配置。
/// </summary>
public class CorporateSetting : CorpEntityBase
{
    /// <summary>配置名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>配置值。</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>配置描述。</summary>
    public string Desc { get; set; } = string.Empty;
}
