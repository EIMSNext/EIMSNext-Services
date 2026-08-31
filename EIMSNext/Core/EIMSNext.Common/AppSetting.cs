using Microsoft.Extensions.Configuration;

namespace EIMSNext.Common
{
    /// <summary>
    /// 配置
    /// </summary>
    public class AppSetting
    {
        public ServiceHostSettings ServiceHost { get; }

        public WebHostSettings WebHost { get; }

        public StorageSettings Storage { get; }

        public IdentityHostSettings IdentityHost { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        public AppSetting(IConfiguration config)
        {
            var serviceHost = config.GetSection("ServiceHost");
            ServiceHost = new ServiceHostSettings
            {
                BaseUrl = serviceHost.GetSection("BaseUrl").Value,
            };

            var webHost = config.GetSection("WebHost");
            WebHost = new WebHostSettings
            {
                BaseUrl = webHost.GetSection("BaseUrl").Value,
            };

            var storage = config.GetSection("Storage");
            Storage = new StorageSettings
            {
                BaseUrl = storage.GetSection("BaseUrl").Value ?? string.Empty,
                LocalPath = storage.GetSection("LocalPath").Value,
                UploadFolder = storage.GetSection("UploadFolder").Value ?? "upload",
                PublicUrl = storage.GetSection("PublicUrl").Value ?? WebHost.BaseUrl,
            };

            var identityHost = config.GetSection("IdentityHost");
            IdentityHost = new IdentityHostSettings
            {
                BaseUrl = identityHost.GetSection("BaseUrl").Value,
                Authority = identityHost.GetSection("Authority").Value,
                Issuer = identityHost.GetSection("Issuer").Value,
                Audience = identityHost.GetSection("Audience").Value,
                RequireHttpsMetadata = bool.TryParse(identityHost.GetSection("RequireHttpsMetadata").Value, out var requireHttpsMetadata)
                    ? requireHttpsMetadata
                    : null,
            };
        }
    }
}
