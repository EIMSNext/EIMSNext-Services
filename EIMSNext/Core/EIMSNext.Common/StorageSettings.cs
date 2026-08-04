namespace EIMSNext.Common;

public class StorageSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? LocalPath { get; set; }
    public string UploadFolder { get; set; } = "upload";
    public string? PublicUrl { get; set; }
}
