using MongoDB.Driver;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Mongo.Query
{
    /// <summary>
    /// Mongo 查询选项，包含过滤、排序、分页等配置。
    /// </summary>
    /// <typeparam name="T">实体类型。</typeparam>
    public class MongoFindOptions<T>
    {
        /// <summary>
        /// 初始化 <see cref="MongoFindOptions{T}"/> 类的新实例。
        /// </summary>
        public MongoFindOptions()
        {
            Filter = Builders<T>.Filter.Empty;
        }

        /// <summary>
        /// 获取或设置过滤定义。
        /// </summary>
        public FilterDefinition<T> Filter { get; set; }

        /// <summary>
        /// 获取或设置排序定义。
        /// </summary>
        public SortDefinition<T>? Sort { get; set; }

        //public ProjectionDefinition<T>? Projection { get; set; }

        /// <summary>
        /// 获取或设置跳过的记录数。
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// 获取或设置返回的记录数。
        /// </summary>
        public int Take { get; set; } = 20;

        /// <summary>
        /// 获取或设置底层查询选项。
        /// </summary>
        public FindOptions? Options { get; set; }

        /// <summary>
        /// 获取有效的返回记录数。
        /// </summary>
        /// <returns>有效的返回记录数。</returns>
        public int GetEffectiveTake()
        {
            return Take <= 0 ? DynamicFindOptions<T>.DefaultTakeWhenUnspecified : Take;
        }

        /// <summary>
        /// 获取有效的跳过记录数。
        /// </summary>
        /// <returns>有效的跳过记录数。</returns>
        public int GetEffectiveSkip()
        {
            return Math.Max(0, Skip);
        }
    }
}
