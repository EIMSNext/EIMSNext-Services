using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EIMSNext.Core.Mongo
{
    /// <summary>
    /// Mongo 数据库上下文接口，提供集合访问与会话管理。
    /// </summary>
    public interface IMongoDbContex : IDisposable
    {
        /// <summary>获取事务配置。</summary>
        MongoDbConfiguration TransactionConfiguration => new();
        /// <summary>
        /// 获取指定实体类型的集合。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <returns>实体集合。</returns>
        IMongoCollection<T> GetCollection<T>();

        /// <summary>
        /// 获取指定名称与实体类型的集合。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="name">集合名称。</param>
        /// <returns>实体集合。</returns>
        IMongoCollection<T> GetCollection<T>(string name);

        /// <summary>
        /// 开启一个新的客户端会话。
        /// </summary>
        /// <returns>客户端会话句柄。</returns>
        IClientSessionHandle StartSession();

        /// <summary>
        /// 异步开启一个新的客户端会话。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>客户端会话句柄。</returns>
        Task<IClientSessionHandle> StartSessionAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Mongo 数据库上下文基类，负责创建客户端与数据库。
    /// </summary>
    public abstract class MongoDbContextBase : IMongoDbContex, IDisposable
    {
        private readonly IMongoClient _client;
        /// <summary>获取事务配置。</summary>
        public MongoDbConfiguration TransactionConfiguration { get; }

        /// <summary>
        /// 使用配置选项初始化 <see cref="MongoDbContextBase"/> 类的新实例。
        /// </summary>
        /// <param name="settings">Mongo 配置选项。</param>
        protected MongoDbContextBase(IOptions<MongoDbConfiguration> settings)
            : this(settings.Value)
        {
        }

        /// <summary>
        /// 使用配置对象初始化 <see cref="MongoDbContextBase"/> 类的新实例。
        /// </summary>
        /// <param name="setting">Mongo 配置对象。</param>
        protected MongoDbContextBase(MongoDbConfiguration setting)
        {
            TransactionConfiguration = setting;
            if (setting.ConnectionString == null)
                throw new ArgumentNullException(nameof(setting), "MongoDbConfiguration.ConnectionString cannot be null.");

            var mongoUrl = MongoUrl.Create(setting.ConnectionString);

            if (setting.Database == null && mongoUrl.DatabaseName == null)
                throw new ArgumentNullException(nameof(setting), "MongoDbConfiguration.Database cannot be null.");

            var clientSettings = MongoClientSettings.FromUrl(mongoUrl);

            if (setting.SslSettings != null)
            {
                clientSettings.SslSettings = setting.SslSettings;
                clientSettings.UseTls = true;
            }

            _client = new MongoClient(clientSettings);
            Database = _client.GetDatabase(setting.Database ?? mongoUrl.DatabaseName);
        }

        /// <summary>
        /// 获取数据库实例。
        /// </summary>
        public IMongoDatabase Database { get; private set; }

        /// <summary>
        /// 获取指定实体类型的集合。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <returns>实体集合。</returns>
        public IMongoCollection<T> GetCollection<T>()
        {
            return Database.GetCollection<T>(typeof(T).Name);
        }

        /// <summary>
        /// 获取指定名称与实体类型的集合。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="name">集合名称。</param>
        /// <returns>实体集合。</returns>
        public IMongoCollection<T> GetCollection<T>(string name)
        {
            return Database.GetCollection<T>(name);
        }

        /// <summary>
        /// 异步开启一个新的客户端会话。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>客户端会话句柄。</returns>
        public Task<IClientSessionHandle> StartSessionAsync(CancellationToken cancellationToken = default)
        {
            return _client.StartSessionAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 开启一个新的客户端会话。
        /// </summary>
        /// <returns>客户端会话句柄。</returns>
        public IClientSessionHandle StartSession()
        {
            return _client.StartSession();
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            // TODO
        }
    }
}
