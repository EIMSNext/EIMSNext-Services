using Microsoft.Extensions.Configuration;

namespace EIMSNext.Common
{
    /// <summary>
    /// 配置
    /// </summary>
    public class AppSetting
    {
        public string? HostUrl { get; }

        public StorageSettings Storage { get; }

        public OAuthSettings OAuth { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        public AppSetting(IConfiguration config)
        {
            HostUrl = config.GetSection("HostUrl").Value;

            var storage = config.GetSection("Storage");
            Storage = new StorageSettings
            {
                BaseUrl = storage.GetSection("BaseUrl").Value ?? string.Empty,
                LocalPath = storage.GetSection("LocalPath").Value,
                UploadFolder = storage.GetSection("UploadFolder").Value ?? "upload",
                PublicUrl = storage.GetSection("PublicUrl").Value ?? HostUrl,
            };

            var oauth = config.GetSection("OAuth");
            OAuth = new OAuthSettings
            {
                BaseUrl = oauth.GetSection("BaseUrl").Value,
                Authority = oauth.GetSection("Authority").Value,
                Issuer = oauth.GetSection("Issuer").Value,
                Audience = oauth.GetSection("Audience").Value,
                RequireHttpsMetadata = bool.TryParse(oauth.GetSection("RequireHttpsMetadata").Value, out var requireHttpsMetadata)
                    ? requireHttpsMetadata
                    : null,
            };
        }
    }
}
