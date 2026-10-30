namespace EIMSNext.ApiClient.File
{
    public interface IFileClient
    {
        /// <summary>
        /// 调用 File.Host /api/v1/Upload 上传附件（复用其扩展名与文件内容校验）。
        /// </summary>
        Task<FileUploadItem?> Upload(byte[] content, string fileName, string accessToken);
    }

    public class FileUploadItem
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string SavePath { get; set; } = string.Empty;
        public string ThumbPath { get; set; } = string.Empty;
        public string FileExt { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    /// <summary>
    /// File.Host /api/v1/Upload 响应外壳。必须保持 public：RestSharp 反序列化私有嵌套类不可靠
    /// （参见 SystemTokenProvider.SystemTokenResponse 的历史事故注释）。
    /// </summary>
    public sealed class FileUploadEnvelope
    {
        public List<FileUploadItem>? Value { get; set; }
    }
}
