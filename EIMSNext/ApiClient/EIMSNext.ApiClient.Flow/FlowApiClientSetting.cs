using EIMSNext.ApiClient.Abstraction;

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
