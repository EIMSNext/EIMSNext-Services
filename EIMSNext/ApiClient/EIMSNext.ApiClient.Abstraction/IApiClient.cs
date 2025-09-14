using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EIMSNext.ApiClient.Abstraction
{
    public interface IApiClient { }
    public interface IApiClientSetting
    {
        bool Verify();
        RetryPolicy RetryPolicy { get; set; }
    }
    public class RetryPolicy
    {
        public bool Enabled { get; set; } = false;
        public int RetryTimes { get; set; }
    }

    public abstract class ApiClientBase<TClient, TSetting> : IApiClient
       where TClient : IApiClient
       where TSetting : IApiClientSetting, new()
    {
        public ApiClientBase(IConfiguration config, ILogger<TClient> logger)
        {
            Setting = new TSetting();
            config.GetSection(SettingSectionName).Bind(Setting);
            Setting.Verify();

            Logger = logger;
        }

        protected TSetting Setting { get; private set; }
        protected ILogger<TClient> Logger { get; private set; }

        protected virtual string SettingSectionName => typeof(TClient).Name;
    }
}
