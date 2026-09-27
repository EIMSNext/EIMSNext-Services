using Microsoft.Extensions.Configuration;

namespace EIMSNext.Common
{
    /// <summary>
    /// 应用程序配置，从 <see cref="IConfiguration"/> 中读取并组合各主机配置。
    /// </summary>
    public class AppSetting
    {
        /// <summary>
        /// 获取服务主机配置。
        /// </summary>
        public ServiceHostSettings ServiceHost { get; }

        /// <summary>
        /// 获取 Web 主机配置。
        /// </summary>
        public WebHostSettings WebHost { get; }

        /// <summary>
        /// 获取文件存储配置。
        /// </summary>
        public StorageSettings Storage { get; }

        /// <summary>
        /// 获取身份认证主机配置。
        /// </summary>
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
