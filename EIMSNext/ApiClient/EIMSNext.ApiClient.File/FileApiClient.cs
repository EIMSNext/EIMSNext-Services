using EIMSNext.ApiClient.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EIMSNext.ApiClient.File
{
    public class FileApiClient : RestApiClientBase<FileApiClient, FileApiClientSetting>, IFileClient
    {
        public FileApiClient(IConfiguration config, ILogger<FileApiClient> logger) : base(config, logger)
        {
        }

        public async Task<FileUploadItem?> Upload(byte[] content, string fileName, string accessToken)
        {
            var response = await UploadFileAsync<FileUploadEnvelope>("api/v1/Upload", content, fileName, accessToken);
            return response.Data?.Value?.FirstOrDefault();
        }
    }
}
