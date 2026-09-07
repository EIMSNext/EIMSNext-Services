using MongoDB.Driver;

namespace EIMSNext.Core.Mongo
{
    /// <summary>
    /// Mongo 数据库配置。
    /// </summary>
    public class MongoDbConfiguration
    {
        /// <summary>连接字符串。</summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>数据库名称。</summary>
        public string Database { get; set; } = string.Empty;

        /// <summary>SSL 设置。</summary>
        public SslSettings? SslSettings { get; set; }
    }
}
