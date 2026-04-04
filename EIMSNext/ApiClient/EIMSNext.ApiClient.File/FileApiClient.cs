using EIMSNext.ApiClient.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EIMSNext.ApiClient.File
{
    public class FileApiClient : RestApiClientBase<FileApiClient, FileApiClientSetting>
    {
        public FileApiClient(IConfiguration config, ILogger<FileApiClient> logger) : base(config, logger)
        {
        }

        public async Task<FileUploadResult?> UploadTemp(byte[] content, string fileName, string accessToken)
        {
            var response = await UploadFileAsync<FileUploadResponse>("Upload/UploadTemp", content, fileName, accessToken);
            var file = response.Data?.Value?.FirstOrDefault();
            if (file == null || string.IsNullOrEmpty(file.SavePath))
            {
                return null;
            }

            return new FileUploadResult
            {
                FileName = file.FileName,
                FileSize = file.FileSize,
                DownloadUrl = $"{Setting.BaseUrl.TrimEnd('/')}/{file.SavePath.TrimStart('/', '\\')}"
            };
        }
    }
}
