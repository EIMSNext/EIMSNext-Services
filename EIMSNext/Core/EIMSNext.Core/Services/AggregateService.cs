using EIMSNext.Core.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 聚合查询服务。
    /// </summary>
    public class AggregateService
    {
        /// <summary>
        /// 初始化 <see cref="AggregateService"/> 类的新实例。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        public AggregateService(IMongoDbContex dbContext)
        {
            DbContext = dbContext;
        }

        private IMongoDbContex DbContext { get; set; }

        /// <summary>
        /// 获取指定名称的集合。
        /// </summary>
        /// <param name="name">集合名称。</param>
        /// <returns>Bson 文档集合。</returns>
        public IMongoCollection<BsonDocument> GetCollection(string name)
        {
            return DbContext.GetCollection<BsonDocument>("FormData");
        }
    }
}
