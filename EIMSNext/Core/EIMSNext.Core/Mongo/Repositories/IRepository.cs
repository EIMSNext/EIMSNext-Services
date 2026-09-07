using System.Linq.Expressions;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace EIMSNext.Core.Mongo.Repositories
{
    /// <summary>
    /// Mongo 仓储接口，定义实体 <typeparamref name="T"/> 的基础查询与增删改操作。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
    public interface IRepository<T> where T : IMongoEntity
    {
        /// <summary>
        /// 获取数据库上下文。
        /// </summary>
        IMongoDbContex DbContext { get; }

        /// <summary>
        /// 获取实体对应的 Mongo 集合。
        /// </summary>
        IMongoCollection<T> Collection { get; }

        /// <summary>
        /// 获取实体的可查询集合。
        /// </summary>
        IQueryable<T> Queryable { get; }

        /// <summary>
        /// 获取过滤器定义构建器。
        /// </summary>
        FilterDefinitionBuilder<T> FilterBuilder { get; }

        /// <summary>
        /// 获取排序定义构建器。
        /// </summary>
        SortDefinitionBuilder<T> SortBuilder { get; }

        /// <summary>
        /// 获取搜索定义构建器。
        /// </summary>
        SearchDefinitionBuilder<T> SearchBuilder { get; }

        /// <summary>
        /// 获取投影定义构建器。
        /// </summary>
        ProjectionDefinitionBuilder<T> ProjectionBuilder { get; }

        /// <summary>
        /// 获取更新定义构建器。
        /// </summary>
        UpdateDefinitionBuilder<T> UpdateBuilder { get; }

        /// <summary>
        /// 创建一个新的 Mongo 事务作用域。
        /// </summary>
        /// <param name="transOptions">事务选项，可为空。</param>
        /// <returns>新的事务作用域实例。</returns>
        MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null);

        /// <summary>
        /// 根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        IFindFluent<T, T> Find(DynamicFindOptions<T> options, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据 Mongo 查询选项查找实体。
        /// </summary>
        /// <param name="options">Mongo 查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        IFindFluent<T, T> Find(MongoFindOptions<T> options, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        IFindFluent<T, T> Find(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据动态查询选项异步查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据 Mongo 查询选项异步查找实体。
        /// </summary>
        /// <param name="options">Mongo 查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        Task<IAsyncCursor<T>> FindAsync(MongoFindOptions<T> options, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据表达式过滤条件异步查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        T? Get(string id, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据主键 ID 异步获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        Task<T?> GetAsync(string id, IClientSessionHandle? session = null);

        /// <summary>
        /// 统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null);

        /// <summary>
        /// 统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        long Count(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null);

        /// <summary>
        /// 统计满足过滤定义条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        long Count(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null);

        /// <summary>
        /// 异步统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null);

        /// <summary>
        /// 异步统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        Task<long> CountAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null);

        /// <summary>
        /// 异步统计满足过滤定义条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        Task<long> CountAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null);

        /// <summary>
        /// 插入单个实体。
        /// </summary>
        /// <param name="entity">要插入的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        void Insert(T entity, IClientSessionHandle? session = null);

        /// <summary>
        /// 批量插入实体。
        /// </summary>
        /// <param name="entities">要插入的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        void Insert(IEnumerable<T> entities, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步插入单个实体。
        /// </summary>
        /// <param name="entity">要插入的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        Task InsertAsync(T entity, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步批量插入实体。
        /// </summary>
        /// <param name="entities">要插入的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        Task InsertAsync(IEnumerable<T> entities, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据主键 ID 更新实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        UpdateResult Update(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步根据主键 ID 更新实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据动态过滤条件批量更新实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        UpdateResult UpdateMany(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步根据动态过滤条件批量更新实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        Task<UpdateResult> UpdateManyAsync(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据过滤定义批量更新实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        UpdateResult UpdateMany(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步根据过滤定义批量更新实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        Task<UpdateResult> UpdateManyAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);

        /// <summary>
        /// 替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>替换结果。</returns>
        public ReplaceOneResult Replace(T entity, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>替换结果。</returns>
        Task<ReplaceOneResult> ReplaceAsync(T entity, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        DeleteResult Delete(string id, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null);

        /// <summary>
        /// 根据过滤定义批量删除实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        DeleteResult Delete(FilterDefinition<T> filter, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步根据过滤定义批量删除实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        Task<DeleteResult> DeleteAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null);

        /// <summary>
        /// 异步获取满足动态过滤条件的字段去重值列表。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="field">要去重的字段名。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>字段去重值列表。</returns>
        Task<List<BsonValue>> DistinctFieldValuesAsync(DynamicFilter filter, string field, IClientSessionHandle? session = null);

        /// <summary>
        /// 为实体集合确保主键 ID 已生成。
        /// </summary>
        /// <param name="entities">实体集合。</param>
        /// <returns>已确保 ID 的实体集合。</returns>
        IEnumerable<T> EnsureId(IEnumerable<T> entities);

        /// <summary>
        /// 为单个实体确保主键 ID 已生成。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <returns>已确保 ID 的实体。</returns>
        T EnsureId(T entity);

        /// <summary>
        /// 生成新的主键 ID。
        /// </summary>
        /// <returns>新生成的主键 ID。</returns>
        string NewId();
    }
}
