using EIMSNext.ApiClient.Contracts;

namespace EIMSNext.ApiClient.Flow
{
    public class FlowApiClientSetting : RestApiClientSetting
    {
        public override bool Verify()
        {
            return !string.IsNullOrEmpty(BaseUrl);
        }
    }
}
