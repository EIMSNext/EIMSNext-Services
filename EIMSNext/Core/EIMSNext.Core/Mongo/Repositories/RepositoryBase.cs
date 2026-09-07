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
    /// 泛型仓储基类，为 <typeparamref name="T"/> 实体提供 MongoDB 的查询、计数、增删改等通用操作。
    /// </summary>
    /// <typeparam name="T">实体类型，必须实现 <see cref="IMongoEntity"/>。</typeparam>
    public abstract class RepositoryBase<T> : IRepository<T> where T : IMongoEntity
    {
        /// <summary>
        /// 使用指定数据库上下文初始化 <see cref="RepositoryBase{T}"/> 类的新实例。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        public RepositoryBase(IMongoDbContex dbContext)
        {
            DbContext = dbContext;
            Collection = dbContext.GetCollection<T>();
        }

        /// <summary>
        /// 获取数据库上下文。
        /// </summary>
        public IMongoDbContex DbContext { get; private set; }

        /// <summary>
        /// 获取当前集合。
        /// </summary>
        public IMongoCollection<T> Collection { get; private set; }

        /// <summary>
        /// 获取可查询对象。
        /// </summary>
        public IQueryable<T> Queryable => Collection.AsQueryable();

        /// <summary>
        /// 获取过滤条件构建器。
        /// </summary>
        public FilterDefinitionBuilder<T> FilterBuilder => Builders<T>.Filter;

        /// <summary>
        /// 获取排序条件构建器。
        /// </summary>
        public SortDefinitionBuilder<T> SortBuilder => Builders<T>.Sort;

        /// <summary>
        /// 获取搜索条件构建器。
        /// </summary>
        public SearchDefinitionBuilder<T> SearchBuilder => Builders<T>.Search;

        /// <summary>
        /// 获取投影条件构建器。
        /// </summary>
        public ProjectionDefinitionBuilder<T> ProjectionBuilder => Builders<T>.Projection;

        /// <summary>
        /// 获取更新条件构建器。
        /// </summary>
        public UpdateDefinitionBuilder<T> UpdateBuilder => Builders<T>.Update;

        #region Public

        /// <summary>
        /// 创建新的事务作用域。
        /// </summary>
        /// <param name="transOptions">可选的事务选项。</param>
        /// <returns>新的事务作用域实例。</returns>
        public MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null)
        {
            return new MongoTransactionScope(DbContext, transOptions);
        }

        /// <summary>
        /// 根据动态查询选项执行查询。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>查询结果流。</returns>
        public virtual IFindFluent<T, T> Find(DynamicFindOptions<T> options, IClientSessionHandle? session = null)
        {
            return FindCore(options, session);
        }

        /// <summary>
        /// 根据表达式过滤条件执行查询。
        /// </summary>
        /// <param name="filter">过滤表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>查询结果流。</returns>
        public virtual IFindFluent<T, T> Find(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null)
        {
            return FindCore(filter, session);
        }

        /// <summary>
        /// 根据查询选项（含过滤、排序、分页）执行查询。
        /// </summary>
        /// <param name="options">查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>查询结果流。</returns>
        public virtual IFindFluent<T, T> Find(MongoFindOptions<T> options, IClientSessionHandle? session = null)
        {
            session = GetSessionHandle(session);
            var result = session == null ? Collection.Find(options.Filter, options.Options) : Collection.Find(session, options.Filter, options.Options);

            if (options.Sort != null)
                result = result.Sort(options.Sort);

            //if (options.Projection != null)
            //    result = result.Project<T>(options.Projection);

            return result.Skip(options.GetEffectiveSkip()).Limit(options.GetEffectiveTake());
        }
        /// <summary>
        /// 异步根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        public virtual Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options, IClientSessionHandle? session = null)
        {
            return FindCoreAsync(options, session);
        }
        /// <summary>
        /// 异步根据 Mongo 查询选项查找实体。
        /// </summary>
        /// <param name="options">Mongo 查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        public virtual async Task<IAsyncCursor<T>> FindAsync(MongoFindOptions<T> options, IClientSessionHandle? session = null)
        {
            return await Find(options, session).ToCursorAsync();
        }
        /// <summary>
        /// 异步根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        public virtual async Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null)
        {
            session = GetSessionHandle(session);
            return await (session == null ? Collection.FindAsync<T>(filter) : Collection.FindAsync(session, filter));
        }

        /// <summary>
        /// 统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountCore(filter.ToFilterDefinition<T>(), session, options);
        }
        /// <summary>
        /// 统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual long Count(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountCore(filter, session, options);
        }
        /// <summary>
        /// 统计满足过滤定义条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual long Count(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountCore(filter, session, options);
        }
        /// <summary>
        /// 异步统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountCoreAsync(filter.ToFilterDefinition<T>(), session, options);
        }
        /// <summary>
        /// 异步统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual Task<long> CountAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountCoreAsync(filter, session, options);
        }
        /// <summary>
        /// 异步统计满足过滤定义条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual Task<long> CountAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountCoreAsync(filter, session, options);
        }

        /// <summary>
        /// 根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        public virtual T? Get(string id, IClientSessionHandle? session = null)
        {
            var idFilter = FilterBuilder.Eq(x => x.Id, id);
            session = GetSessionHandle(session);
            var result = session == null ? Collection.Find(idFilter) : Collection.Find(session, idFilter);
            return result.FirstOrDefault();
        }
        /// <summary>
        /// 异步根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        public virtual async Task<T?> GetAsync(string id, IClientSessionHandle? session = null)
        {
            var idFilter = FilterBuilder.Eq(x => x.Id, id);
            session = GetSessionHandle(session);
            var result = await (session == null ? Collection.FindAsync(idFilter) : Collection.FindAsync(session, idFilter));
            return result.FirstOrDefault();
        }

        /// <summary>
        /// 插入单个实体。
        /// </summary>
        /// <param name="entity">要插入的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        public virtual void Insert(T entity, IClientSessionHandle? session = null)
        {
            InsertCore(EnsureId(entity), session);
        }
        /// <summary>
        /// 批量插入实体。
        /// </summary>
        /// <param name="entities">要插入的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        public virtual void Insert(IEnumerable<T> entities, IClientSessionHandle? session = null)
        {
            InsertCore(EnsureId(entities), session);
        }
        /// <summary>
        /// 异步插入单个实体。
        /// </summary>
        /// <param name="entity">要插入的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        public virtual Task InsertAsync(T entity, IClientSessionHandle? session = null)
        {
            return InsertCoreAsync(EnsureId(entity), session);
        }
        /// <summary>
        /// 异步批量插入实体。
        /// </summary>
        /// <param name="entities">要插入的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        public virtual Task InsertAsync(IEnumerable<T> entities, IClientSessionHandle? session = null)
        {
            return InsertCoreAsync(EnsureId(entities), session);
        }

        /// <summary>
        /// 根据主键 ID 更新实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        public virtual UpdateResult Update(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCore(FilterBuilder.Eq(x => x.Id, id), update, false, upsert, session);
        }
        /// <summary>
        /// 异步根据主键 ID 更新实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        public virtual Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCoreAsync(FilterBuilder.Eq(x => x.Id, id), update, false, upsert, session);
        }
        /// <summary>
        /// 根据动态过滤条件批量更新实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        public virtual UpdateResult UpdateMany(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCore(filter.ToFilterDefinition<T>(), update, true, upsert, session);
        }
        /// <summary>
        /// 异步根据动态过滤条件批量更新实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        public virtual Task<UpdateResult> UpdateManyAsync(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCoreAsync(filter.ToFilterDefinition<T>(), update, true, upsert, session);
        }
        /// <summary>
        /// 根据过滤定义批量更新实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        public virtual UpdateResult UpdateMany(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCore(filter, update, true, upsert, session);
        }
        /// <summary>
        /// 异步根据过滤定义批量更新实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入，默认 true。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        public virtual Task<UpdateResult> UpdateManyAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCoreAsync(filter, update, true, upsert, session);
        }

        /// <summary>
        /// 替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>替换结果。</returns>
        public ReplaceOneResult Replace(T entity, IClientSessionHandle? session = null)
        {
            return ReplaceCore(entity, session);
        }
        /// <summary>
        /// 异步替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>替换结果。</returns>
        public virtual Task<ReplaceOneResult> ReplaceAsync(T entity, IClientSessionHandle? session = null)
        {
            return ReplaceCoreAsync(entity, session);
        }

        /// <summary>
        /// 根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        public virtual DeleteResult Delete(string id, IClientSessionHandle? session = null)
        {
            return DeleteCore(FilterBuilder.Eq(x => x.Id, id), session);
        }
        /// <summary>
        /// 根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        public virtual DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null)
        {
            return DeleteCore(FilterBuilder.In(x => x.Id, ids), session);
        }
        /// <summary>
        /// 根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        public virtual DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null)
        {
            return DeleteCore(filter.ToFilterDefinition<T>(), session);
        }
        /// <summary>
        /// 根据过滤定义批量删除实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        public virtual DeleteResult Delete(FilterDefinition<T> filter, IClientSessionHandle? session = null)
        {
            return DeleteCore(filter, session);
        }

        /// <summary>
        /// 异步根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        public virtual Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null)
        {
            return DeleteCoreAsync(FilterBuilder.Eq(x => x.Id, id), session);
        }
        /// <summary>
        /// 异步根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        public virtual Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null)
        {
            return DeleteCoreAsync(FilterBuilder.In(x => x.Id, ids), session);
        }
        /// <summary>
        /// 异步根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        public virtual Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null)
        {
            return DeleteCoreAsync(filter.ToFilterDefinition<T>(), session);
        }
        /// <summary>
        /// 异步根据过滤定义批量删除实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        public virtual Task<DeleteResult> DeleteAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null)
        {
            return DeleteCoreAsync(filter, session);
        }

        #endregion

        #region Core  

        /// <summary>
        /// 根据动态查询选项查找实体（核心实现）。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        protected virtual IFindFluent<T, T> FindCore(DynamicFindOptions<T> options, IClientSessionHandle? session)
        {
            var filter = options.Filter == null ? FilterBuilder.Empty : options.Filter.ToFilterDefinition<T>();
            session = GetSessionHandle(session);
            var result = session == null ? Collection.Find(filter) : Collection.Find(session, filter);

            var sort = options.Sort?.ToSortDefinition<T>();
            if (sort != null)
                result = result.Sort(sort);

            var projection = options.Select?.ToProjectionDefinition<T>();
            if (projection != null)
                result = result.Project<T>(projection);

            return result.Skip(options.GetEffectiveSkip()).Limit(options.GetEffectiveTake());
        }
        /// <summary>
        /// 根据表达式过滤条件查找实体（核心实现）。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        protected virtual IFindFluent<T, T> FindCore(Expression<Func<T, bool>> filter, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            return session == null ? Collection.Find(filter) : Collection.Find(session, filter);
        }
        /// <summary>
        /// 异步根据动态查询选项查找实体（核心实现）。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        protected virtual async Task<IAsyncCursor<T>> FindCoreAsync(DynamicFindOptions<T> options, IClientSessionHandle? session)
        {
            return await FindCore(options, session).ToCursorAsync();
        }
        /// <summary>
        /// 异步根据表达式过滤条件查找实体（核心实现）。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        protected virtual async Task<IAsyncCursor<T>> FindCoreAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            return await (session == null ? Collection.FindAsync(filter) : Collection.FindAsync(session, filter));
        }

        /// <summary>
        /// 统计满足表达式过滤条件的实体数量（核心实现）。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual long CountCore(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            session = GetSessionHandle(session);
            return (session == null ? Collection.CountDocuments(filter, options) : Collection.CountDocuments(session, filter, options));
        }
        /// <summary>
        /// 统计满足过滤定义条件的实体数量（核心实现）。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual long CountCore(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            session = GetSessionHandle(session);
            return (session == null ? Collection.CountDocuments(filter, options) : Collection.CountDocuments(session, filter, options));
        }
        /// <summary>
        /// 异步统计满足表达式过滤条件的实体数量（核心实现）。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual Task<long> CountCoreAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            session = GetSessionHandle(session);
            return (session == null ? Collection.CountDocumentsAsync(filter, options) : Collection.CountDocumentsAsync(session, filter, options));
        }
        /// <summary>
        /// 异步统计满足过滤定义条件的实体数量（核心实现）。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <param name="options">统计选项。</param>
        /// <returns>实体数量。</returns>
        public virtual Task<long> CountCoreAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            session = GetSessionHandle(session);
            return (session == null ? Collection.CountDocumentsAsync(filter, options) : Collection.CountDocumentsAsync(session, filter, options));
        }

        /// <summary>
        /// 插入单个实体（核心实现）。
        /// </summary>
        /// <param name="entity">要插入的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual void InsertCore(T entity, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                Collection.InsertOne(entity);
            else
                Collection.InsertOne(session, entity);
        }
        /// <summary>
        /// 批量插入实体（核心实现）。
        /// </summary>
        /// <param name="entities">要插入的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual void InsertCore(IEnumerable<T> entities, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                Collection.InsertMany(entities);
            else
                Collection.InsertMany(session, entities);
        }
        /// <summary>
        /// 异步插入单个实体（核心实现）。
        /// </summary>
        /// <param name="entity">要插入的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task InsertCoreAsync(T entity, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.InsertOneAsync(entity);
            else
                return Collection.InsertOneAsync(session, entity);
        }
        /// <summary>
        /// 异步批量插入实体（核心实现）。
        /// </summary>
        /// <param name="entities">要插入的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task InsertCoreAsync(IEnumerable<T> entities, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.InsertManyAsync(entities);
            else
                return Collection.InsertManyAsync(session, entities);
        }

        /// <summary>
        /// 更新实体（核心实现）。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="many">是否批量更新。</param>
        /// <param name="upsert">不存在时是否插入。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        protected virtual UpdateResult UpdateCore(FilterDefinition<T> filter, UpdateDefinition<T> update, bool many, bool upsert, IClientSessionHandle? session)
        {
            var options = new UpdateOptions { IsUpsert = upsert, BypassDocumentValidation = true };
            session = GetSessionHandle(session);
            if (session == null)
                return many ? Collection.UpdateMany(filter, update, options) : Collection.UpdateOne(filter, update, options);
            else
                return many ? Collection.UpdateMany(session, filter, update, options) : Collection.UpdateOne(session, filter, update, options);
        }
        /// <summary>
        /// 异步更新实体（核心实现）。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="many">是否批量更新。</param>
        /// <param name="upsert">不存在时是否插入。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        protected virtual Task<UpdateResult> UpdateCoreAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool many, bool upsert, IClientSessionHandle? session)
        {
            var options = new UpdateOptions { IsUpsert = upsert, BypassDocumentValidation = true };
            session = GetSessionHandle(session);
            if (session == null)
                return many ? Collection.UpdateManyAsync(filter, update, options) : Collection.UpdateOneAsync(filter, update, options);
            else
                return many ? Collection.UpdateManyAsync(session, filter, update, options) : Collection.UpdateOneAsync(session, filter, update, options);
        }

        /// <summary>
        /// 替换单个实体（核心实现）。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>替换结果。</returns>
        protected virtual ReplaceOneResult ReplaceCore(T entity, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.ReplaceOne(GetIdFilter(entity), entity);
            else
                return Collection.ReplaceOne(session, GetIdFilter(entity), entity);
        }
        /// <summary>
        /// 异步替换单个实体（核心实现）。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>替换结果。</returns>
        protected virtual Task<ReplaceOneResult> ReplaceCoreAsync(T entity, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.ReplaceOneAsync(GetIdFilter(entity), entity);
            else
                return Collection.ReplaceOneAsync(session, GetIdFilter(entity), entity);
        }

        /// <summary>
        /// 批量删除实体（核心实现）。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        protected virtual DeleteResult DeleteCore(FilterDefinition<T> filter, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.DeleteMany(filter);
            else
                return Collection.DeleteMany(session, filter);
        }
        /// <summary>
        /// 异步批量删除实体（核心实现）。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果。</returns>
        protected virtual Task<DeleteResult> DeleteCoreAsync(FilterDefinition<T> filter, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.DeleteManyAsync(filter);
            else
                return Collection.DeleteManyAsync(session, filter);
        }

        /// <summary>
        /// 异步获取满足动态过滤条件的字段去重值列表。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <param name="field">要去重的字段名。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>字段去重值列表。</returns>
        public virtual async Task<List<BsonValue>> DistinctFieldValuesAsync(DynamicFilter filter, string field, IClientSessionHandle? session = null)
        {
            var bsonFilter = filter.ToFilterDefinition<T>();
            session = GetSessionHandle(session);
            using var cursor = await (session == null ? Collection.DistinctAsync<BsonValue>(field, bsonFilter) : Collection.DistinctAsync<BsonValue>(session, field, bsonFilter));
            return await cursor.ToListAsync();
        }

        #endregion

        #region Helper

        /// <summary>
        /// 为实体集合确保主键 ID 已生成。
        /// </summary>
        /// <param name="entities">实体集合。</param>
        /// <returns>已确保 ID 的实体集合。</returns>
        public IEnumerable<T> EnsureId(IEnumerable<T> entities)
        {
            entities.ForEach(x => EnsureId(x));
            return entities;
        }

        /// <summary>
        /// 为单个实体确保主键 ID 已生成。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <returns>已确保 ID 的实体。</returns>
        public T EnsureId(T entity)
        {
            if (string.IsNullOrEmpty(entity.Id))
            {
                entity.Id = NewId();
            }
            return entity;
        }
        /// <summary>
        /// 生成新的主键 ID。
        /// </summary>
        /// <returns>新生成的主键 ID。</returns>
        public string NewId()
        {
            return ObjectId.GenerateNewId().ToString();
        }

        /// <summary>
        /// 根据实体的主键 ID 构建过滤定义。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <returns>过滤定义。</returns>
        protected FilterDefinition<T> GetIdFilter(T entity)
        {
            return FilterBuilder.Eq(x => x.Id, entity.Id);
        }

        private IClientSessionHandle? GetSessionHandle(IClientSessionHandle? session)
        {
            if (session != null) return session;
            if (MongoTransactionScope.IsInTransaction) return MongoTransactionScope.Transaction;
            return null;
        }

        #endregion
    }
}
