using EIMSNext.ApiService.RequestModels;

namespace EIMSNext.ApiService.RequestModels;

/// <summary>
/// 企业设置请求。
/// </summary>
public sealed class CorporateSettingRequest : RequestBase
{
    /// <summary>设置名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>设置值。</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>设置描述。</summary>
    public string Desc { get; set; } = string.Empty;
}
