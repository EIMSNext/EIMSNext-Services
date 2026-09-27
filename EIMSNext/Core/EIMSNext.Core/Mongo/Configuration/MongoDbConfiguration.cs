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

        /// <summary>事务默认读取关注级别。</summary>
        public string TransactionReadConcern { get; set; } = "majority";
        /// <summary>事务默认写入关注级别。</summary>
        public string TransactionWriteConcern { get; set; } = "majority";
        /// <summary>事务瞬态冲突最大重试次数。</summary>
        public int TransactionMaxRetries { get; set; } = 1;
    }
}
