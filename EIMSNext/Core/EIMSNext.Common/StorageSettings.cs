namespace EIMSNext.Common;

/// <summary>
/// 文件存储配置。
/// </summary>
public class StorageSettings
{
    /// <summary>
    /// 获取或设置存储服务的基础 URL。
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置本地存储路径。
    /// </summary>
    public string? LocalPath { get; set; }

    /// <summary>
    /// 获取或设置上传目录，默认为 "upload"。
    /// </summary>
    public string UploadFolder { get; set; } = "upload";

    /// <summary>
    /// 获取或设置公开访问 URL。
    /// </summary>
    public string? PublicUrl { get; set; }
}