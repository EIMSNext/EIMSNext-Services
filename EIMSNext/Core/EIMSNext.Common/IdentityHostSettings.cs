namespace EIMSNext.Common;

/// <summary>
/// 身份认证主机配置。
/// </summary>
public class IdentityHostSettings
{
    /// <summary>
    /// 获取或设置身份认证服务的基础 URL。
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 获取或设置授权服务器地址。
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// 获取或设置令牌签发方。
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// 获取或设置令牌受众。
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// 获取或设置是否要求使用 HTTPS 获取元数据。
    /// </summary>
    public bool? RequireHttpsMetadata { get; set; }

    /// <summary>
    /// 获取用户令牌端点 URL。
    /// </summary>
    public string? TokenEndPoint => BuildEndpoint("connect/token");

    /// <summary>
    /// 获取系统令牌端点 URL。
    /// </summary>
    public string? SystemTokenEndPoint => BuildEndpoint("system/token");

    private string? BuildEndpoint(string relativePath)
    {
        var baseUrl = BaseUrl ?? Authority;
        return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl.TrimEnd('/')}/{relativePath}";
    }
}