using Microsoft.Extensions.Configuration;

namespace EIMSNext.Common
{
    /// <summary>
    /// 配置
    /// </summary>
    public class AppSetting
    {
        private readonly IConfiguration _config;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        public AppSetting(IConfiguration config)
        {
            _config = config;
        }
        /// <summary>
        /// 当前网站RootUrl
        /// </summary>
        public string? HostUrl => _config.GetSection("HostUrl").Value;
        /// <summary>
        /// 上传文件存储目录
        /// </summary>
        public string FileBasePath => _config.GetSection("FileBasePath").Value ?? "upload";
        /// <summary>
        /// 文件服务器地址
        /// </summary>
        public string? FileHostUrl => _config.GetSection("FileHostUrl").Value ?? HostUrl;
        /// <summary>
        /// OAuth的Authority
        /// </summary>
        public string? OAuth_Authority => _config.GetSection("OAuth:Authority").Value;
        /// <summary>
        /// OAuth的TokenEndPoint
        /// </summary>
        public string? OAuth_TokenEndPoint => _config.GetSection("OAuth:TokenEndPoint").Value;
    }
}
