using EIMSNext.ApiClient.Contracts;

namespace EIMSNext.ApiClient.File
{
    public class FileApiClientSetting : RestApiClientSetting
    {
        public override bool Verify()
        {
            return !string.IsNullOrEmpty(BaseUrl);
        }
    }
}
