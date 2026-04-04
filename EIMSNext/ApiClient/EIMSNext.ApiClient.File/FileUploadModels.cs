namespace EIMSNext.ApiClient.File
{
    public class FileUploadResponse
    {
        public List<UploadedTempFile> Value { get; set; } = new();
    }

    public class UploadedTempFile
    {
        public string FileName { get; set; } = string.Empty;
        public string SavePath { get; set; } = string.Empty;
        public string? FileExt { get; set; }
        public long FileSize { get; set; }
    }

    public class FileUploadResult
    {
        public string DownloadUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }
}
