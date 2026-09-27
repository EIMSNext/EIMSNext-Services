using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Services.Extensions;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 服务基类，提供基于 Mongo 仓储的通用查询与增删改操作、审计日志、缓存及业务钩子。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
    public abstract class ServiceCore<T> where T : class, IMongoEntity
    {
        #region Variables

        /// <summary>
        /// <see cref="IEntity"/> 接口类型。
        /// </summary>
        protected static readonly Type IEntityType = typeof(IEntity);

        /// <summary>
        /// <see cref="IDeleteFlag"/> 接口类型。
        /// </summary>
        protected static readonly Type IDeleteFlagType = typeof(IDeleteFlag);

        /// <summary>
        /// <see cref="ICorpOwned"/> 接口类型。
        /// </summary>
        protected static readonly Type ICorpOwnedType = typeof(ICorpOwned);

        #endregion 

        /// <summary>
        /// 初始化 <see cref="ServiceCore{T}"/> 的新实例。
        /// </summary>
        /// <param name="resolver">依赖解析器。</param>
        protected ServiceCore(IResolver resolver)
        {
            Resolver = resolver;
            Repository = resolver.GetRepository<T>();
            AuditLogRepository = resolver.GetRepository<AuditLog>();
            CacheClient = resolver.GetCacheClient();
            Logger = resolver.GetLogger<T>();
            Context = resolver.GetServiceContext();
            ScopeCache = resolver.Resolve<IScopeCache>();
        }

        #region Properties

        /// <summary>
        /// 获取依赖解析器。
        /// </summary>
        protected IResolver Resolver { get; private set; }

        /// <summary>
        /// 获取实体 <typeparamref name="T"/> 对应的仓储。
        /// </summary>
        protected IRepository<T> Repository { get; private set; }

        /// <summary>
        /// 获取审计日志仓储。
        /// </summary>
        protected IRepository<AuditLog> AuditLogRepository { get; private set; }

        /// <summary>
        /// 获取缓存客户端。
        /// </summary>
        protected ICacheClient CacheClient { get; private set; }

        /// <summary>
        /// 获取日志记录器。
        /// </summary>
        protected ILogger<T> Logger { get; private set; }

        /// <summary>
        /// 获取服务上下文。
        /// </summary>
        protected IServiceContext Context { get; private set; }

        /// <summary>
        /// 获取作用域缓存。
        /// </summary>
        protected IScopeCache ScopeCache { get; private set; }

        /// <summary>
        /// 获取一个值，指示删除操作是否采用逻辑删除。
        /// </summary>
        protected virtual bool LogicDelete => true;

        /// <summary>
        /// 获取一个值，指示是否记录审计日志。
        /// </summary>
        protected virtual bool LogAudit => true;

        /// <summary>
        /// 获取一个值，指示通用增删改操作是否在 root scope 中启用事务。
        /// 已存在的外层事务始终由内层操作继承。
        /// </summary>
        protected virtual bool TransNeeded => true;

        /// <summary>
        /// 获取过滤器定义构建器。
        /// </summary>
        protected FilterDefinitionBuilder<T> FilterBuilder => Repository.FilterBuilder;

        /// <summary>
        /// 获取排序定义构建器。
        /// </summary>
        protected SortDefinitionBuilder<T> SortBuilder => Repository.SortBuilder;

        /// <summary>
        /// 获取搜索定义构建器。
        /// </summary>
        protected SearchDefinitionBuilder<T> SearchBuilder => Repository.SearchBuilder;

        /// <summary>
        /// 获取投影定义构建器。
        /// </summary>
        protected ProjectionDefinitionBuilder<T> ProjectionBuilder => Repository.ProjectionBuilder;

        /// <summary>
        /// 获取更新定义构建器。
        /// </summary>
        protected UpdateDefinitionBuilder<T> UpdateBuilder => Repository.UpdateBuilder;

        #endregion

        #region Helper

        /// <summary>
        /// 创建一个新的 Mongo 事务作用域。
        /// </summary>
        /// <param name="transOptions">事务选项，可为空。</param>
        /// <returns>新的事务作用域实例。</returns>
        protected MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null)
        {
            return new MongoTransactionScope(Repository.DbContext, transOptions, TransNeeded);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="operation"></param>
        /// <param name="options"></param>
        /// <param name="maxRetries"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected Task<TResult> ExecuteWithTransactionRetryAsync<TResult>(Func<IClientSessionHandle, Task<TResult>> operation, TransactionOptions? options = null, int? maxRetries = null, CancellationToken cancellationToken = default)
            => MongoTransactionScope.ExecuteWithRetryAsync(Repository.DbContext, operation, options, maxRetries, cancellationToken);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="options"></param>
        /// <param name="maxRetries"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected Task ExecuteWithTransactionRetryAsync(Func<IClientSessionHandle, Task> operation, TransactionOptions? options = null, int? maxRetries = null, CancellationToken cancellationToken = default)
            => MongoTransactionScope.ExecuteWithRetryAsync(Repository.DbContext, operation, options, maxRetries, cancellationToken);

        /// <summary>
        /// 记录审计日志。
        /// </summary>
        /// <param name="action">数据库操作类型。</param>
        /// <param name="oldData">变更前的实体集合。</param>
        /// <param name="newData">变更后的实体集合。</param>
        /// <param name="filter">操作对应的过滤条件。</param>
        /// <param name="update">操作对应的更新定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual void CreateAuditLog(DbAction action, IEnumerable<T>? oldData, IEnumerable<T>? newData, FilterDefinition<T>? filter, UpdateDefinition<T>? update, IClientSessionHandle? session)
        {
            if (!LogAudit) return;

            var logList = new List<AuditLog>();
            if (action == DbAction.Insert && newData != null)
            {
                logList = CreateInsertLog(newData);
            }
            else if (action == DbAction.Update)
            {
                logList = CreateUpdateLog(oldData, newData, filter, update);
            }
            else if (action == DbAction.Delete)
            {
                logList = CreateDeleteLog(oldData, filter);
            }

            if (logList.Count > 0)
            {
                if (session != null)
                {
                    // TODO: 后续改为分布式审计队列，确保审计失败可重试且不阻塞业务事务。
                    MongoTransactionScope.RegisterAfterCommitAsync(async () =>
                    {
                        try
                        {
                            await AuditLogRepository.InsertAsync(logList).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "事务提交后的审计日志写入失败，实体类型: {EntityType}", typeof(T).Name);
                        }
                    });
                }
                else
                {
                    AuditLogRepository.Insert(logList);
                }
            }
        }

        /// <summary>
        /// 根据新增数据构建插入审计日志。
        /// </summary>
        /// <param name="newData">新增的实体集合。</param>
        /// <returns>插入审计日志列表。</returns>
        protected virtual List<AuditLog> CreateInsertLog(IEnumerable<T> newData)
        {
            var logList = new List<AuditLog>();
            var now = DateTime.UtcNow.ToTimeStampMs();
            var op = Context.Operator;
            var ip = Context.ClientIp;
            var corpId = Context.CorpId;
             newData.ForEach(x => logList.Add(
             new AuditLog
             {
                 Action = DbAction.Insert,
                 EntityType = typeof(T).Name,
                 DataId = x.Id,
                 Detail = $"新增数据:", //TODO:考虑显示一两个主字段？
                 NewData = x.SerializeToJson(),
                 CreateBy = op,
                 UpdateBy = op,
                 CreateTime = now,
                 UpdateTime = now,
                 ClientIp = ip,
                 CorpId = corpId,
             }));
            return logList;
        }
        /// <summary>
        /// 根据更新前后的数据构建更新审计日志。
        /// </summary>
        /// <param name="oldData">变更前的实体集合。</param>
        /// <param name="newData">变更后的实体集合。</param>
        /// <param name="filter">更新对应的过滤条件。</param>
        /// <param name="update">更新定义。</param>
        /// <returns>更新审计日志列表。</returns>
        protected virtual List<AuditLog> CreateUpdateLog(IEnumerable<T>? oldData, IEnumerable<T>? newData, FilterDefinition<T>? filter, UpdateDefinition<T>? update)
        {
            var logList = new List<AuditLog>();
            var now = DateTime.UtcNow.ToTimeStampMs();
            var op = Context.Operator;
            var ip = Context.ClientIp;
            var corpId = Context.CorpId;

            if (oldData == null || newData == null)
            {
                logList.Add(new AuditLog
                {
                    Action = DbAction.Update,
                    EntityType = typeof(T).Name,
                    Detail = $"批量更新数据(无旧对象):{filter?.ToString()}",
                    DataFilter = filter?.ToString(),
                    CreateBy = op,
                    UpdateBy = op,
                    CreateTime = now,
                    UpdateTime = now,
                    ClientIp = ip,
                    CorpId = corpId,
                });
            }
            else
            {
                oldData.ForEach(x =>
                {
                    var y = newData.FirstOrDefault(e => e.Id == x.Id);
                    if (y != null)
                    {
                        logList.Add(new AuditLog
                        {
                            Action = DbAction.Update,
                            EntityType = typeof(T).Name,
                            DataId = x.Id,
                            Detail = GetChangeDetail(x, y),
                            OldData = x.SerializeToJson(),
                            NewData = y.SerializeToJson(),
                            CreateBy = op,
                            UpdateBy = op,
                            CreateTime = now,
                            UpdateTime = now,
                            ClientIp = ip,
                            CorpId = corpId,
                        });
                    }
                });
            }

            return logList;
        }
        /// <summary>
        /// 根据删除前的数据构建删除审计日志。
        /// </summary>
        /// <param name="oldData">删除前的实体集合。</param>
        /// <param name="filter">删除对应的过滤条件。</param>
        /// <returns>删除审计日志列表。</returns>
        protected virtual List<AuditLog> CreateDeleteLog(IEnumerable<T>? oldData, FilterDefinition<T>? filter)
        {
            var logList = new List<AuditLog>();
            var now = DateTime.UtcNow.ToTimeStampMs();
            var op = Context.Operator;
            var ip = Context.ClientIp;
            var corpId = Context.CorpId;

            if (oldData == null)
            {
                logList.Add(new AuditLog
                {
                    Action = DbAction.Delete,
                    EntityType = typeof(T).Name,
                    Detail = $"批量删除数据:",
                    DataFilter = filter?.ToString(),
                    CreateBy = op,
                    UpdateBy = op,
                    CreateTime = now,
                    UpdateTime = now,
                    ClientIp = ip,
                    CorpId = corpId,
                });
            }
            else
            {
                oldData.ForEach(x =>
                logList.Add(new AuditLog
                {
                    Action = DbAction.Delete,
                    EntityType = typeof(T).Name,
                    DataId = x.Id,
                    Detail = $"删除数据:", //TODO:考虑显示一两个主字段？
                    OldData = x.SerializeToJson(),
                    CreateBy = op,
                    UpdateBy = op,
                    CreateTime = now,
                    UpdateTime = now,
                    ClientIp = ip,
                    CorpId = corpId,
                }));
            }

            return logList;
        }
        /// <summary>
        /// 从作用域缓存中获取实体，缓存未命中时从仓储读取。
        /// </summary>
        /// <typeparam name="S">实体类型。</typeparam>
        /// <param name="key">缓存键。</param>
        /// <param name="version">数据版本。</param>
        /// <returns>缓存的实体，未找到时为 null。</returns>
        protected virtual S? GetFromStore<S>(string key, DataVersion version = DataVersion.Temp) where S : class, IMongoEntity
        {
            return ScopeCache.Get<S>(key, version, id => Resolver.GetRepository<S>().Get(id));
        }

        #region Methods

        /// <summary>
        /// 根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        protected virtual T? GetCore(string id, IClientSessionHandle? session)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Repository.Get(id, session);
        }
        /// <summary>
        /// 根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        protected virtual IFindFluent<T, T> FindCore(DynamicFindOptions<T> options, IClientSessionHandle? session)
        {
            return Repository.Find(options, session);
        }
        /// <summary>
        /// 根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        protected virtual IFindFluent<T, T> FindCore(Expression<Func<T, bool>> filter, IClientSessionHandle? session)
        {
            return Repository.Find(filter, session);
        }

        /// <summary>
        /// 统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        protected virtual long CountCore(DynamicFilter filter)
        {
            return Repository.Count(filter);
        }
        /// <summary>
        /// 统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        protected virtual long CountCore(Expression<Func<T, bool>> filter)
        {
            return Repository.Count(filter);
        }

        /// <summary>
        /// 判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        protected virtual bool ExistsCore(Expression<Func<T, bool>> where, IClientSessionHandle? session)
        {
            return Repository.Find(where, session).CountDocuments() > 0;
        }
        /// <summary>
        /// 判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        protected virtual bool ExistsCore(DynamicFilter where, IClientSessionHandle? session)
        {
            return Repository.Find(new DynamicFindOptions<T> { Filter = where }, session).CountDocuments() > 0;
        }

        //protected virtual void AddCore(T entity, IClientSessionHandle? session)
        //{
        //    FillSystemField(entity, false);
        //    Repository.Insert(entity, session);
        //}
        /// <summary>
        /// 批量新增实体。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual void AddCore(IEnumerable<T> entities, IClientSessionHandle? session)
        {
            entities.ForEach(entity => FillSystemField(entity, false));
            BeforeAdd(entities, session).Wait();
            Repository.Insert(entities, session);
            CreateAuditLog(DbAction.Insert, null, entities, null, null, session);
            AfterAdd(entities, session).Wait();
        }
        /// <summary>
        /// 替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>替换结果。</returns>
        protected virtual ReplaceOneResult ReplaceCore(T entity, IClientSessionHandle? session)
        {
            var entityId = entity.Id;
            FillSystemField(entity, true);
            BeforeReplace(entity, session).Wait();
            var old = ScopeCache.Get<T>(entityId, DataVersion.Old) ?? GetCore(entityId, session);
            var result = Repository.Replace(entity, session);
            CreateAuditLog(DbAction.Update, old == null ? null : [old], [entity], null, null, session);
            AfterReplace(entity, session).Wait();
            return result;
        }
        //protected virtual UpdateResult PatchCore(string id, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        //{
        //    update = FillSystemField(update);
        //    return Repository.Update(id, update, upsert, session);
        //}
        //protected virtual UpdateResult PatchManyCore(DynamicFilter filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        //{
        //    update = FillSystemField(update);
        //    return Repository.UpdateMany(filter, update, upsert, session);
        //}
        /// <summary>
        /// 根据过滤定义批量更新实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        protected virtual UpdateResult PatchManyCore(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        {
            update = FillSystemField(update);
            BeforeUpdate(filter, update, upsert, session).Wait();
            var result = Repository.UpdateMany(filter, update, upsert, session);
            CreateAuditLog(DbAction.Update, null, null, filter, update, session);
            AfterUpdate(filter, update, upsert, session).Wait();
            return result;
        }
        //protected virtual DeleteResult DeleteCore(string id, IClientSessionHandle? session)
        //{
        //    return Repository.Delete(id, session);
        //}
        //protected virtual DeleteResult DeleteCore(IEnumerable<string> ids, IClientSessionHandle? session)
        //{
        //    return Repository.Delete(ids, session);
        //}
        //protected virtual DeleteResult DeleteCore(DynamicFilter filter, IClientSessionHandle? session)
        //{
        //    return Repository.Delete(filter, session);
        //}

        /// <summary>
        /// 根据过滤定义删除实体（支持逻辑删除）。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果对象。</returns>
        protected virtual object DeleteCore(FilterDefinition<T> filter, IClientSessionHandle? session)
        {
            object result = new object();
            BeforeDelete(filter, session).Wait();
            if (LogicDelete && IDeleteFlagType.IsAssignableFrom(typeof(T)))
            {
                var update = UpdateBuilder.Set(Fields.DeleteFlag, true);
                result = Repository.UpdateMany(filter, update, session: session);
            }
            else
                result = Repository.Delete(filter, session);
            CreateAuditLog(DbAction.Delete, null, null, filter, null, session);
            AfterDelete(filter, session).Wait();
            return result;
        }

        private string GetChangeDetail(T oldT, T newT)
        {
            JsonObject? oldJson = null;
            JsonObject? newJson = null;
            try { oldJson = JsonNode.Parse(oldT.SerializeToJson())?.AsObject(); } catch { }
            try { newJson = JsonNode.Parse(newT.SerializeToJson())?.AsObject(); } catch { }

            if (oldJson == null || newJson == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in oldJson) keys.Add(kv.Key);
            foreach (var kv in newJson) keys.Add(kv.Key);

            foreach (var key in keys)
            {
                bool hasOld = oldJson.TryGetPropertyValue(key, out var oldNode);
                bool hasNew = newJson.TryGetPropertyValue(key, out var newNode);

                // 双侧存在且 deep-equal → 跳过未变更字段
                if (hasOld && hasNew && JsonNode.DeepEquals(oldNode, newNode))
                {
                    continue;
                }

                var oldStr = hasOld ? (oldNode?.ToJsonString() ?? "null") : "null";
                var newStr = hasNew ? (newNode?.ToJsonString() ?? "null") : "null";
                builder.Append($"{key}:{oldStr}->{newStr},");
            }

            if (builder.Length > 0) builder.Remove(builder.Length - 1, 1);
            return builder.ToString();
        }

        #endregion

        #region Async Methods

        /// <summary>
        /// 异步根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        protected virtual Task<T?> GetCoreAsync(string id, IClientSessionHandle? session)
        {
            return Repository.GetAsync(id, session);
        }
        /// <summary>
        /// 异步根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        protected virtual Task<IAsyncCursor<T>> FindCoreAsync(DynamicFindOptions<T> options, IClientSessionHandle? session)
        {
            return Repository.FindAsync(options, session);
        }
        /// <summary>
        /// 异步根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>异步游标。</returns>
        protected virtual Task<IAsyncCursor<T>> FindCoreAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session)
        {
            return Repository.FindAsync(filter, session);
        }
        /// <summary>
        /// 异步统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        protected virtual Task<long> CountCoreAsync(DynamicFilter filter)
        {
            return Repository.CountAsync(filter);
        }
        /// <summary>
        /// 异步统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        protected virtual Task<long> CountCoreAsync(Expression<Func<T, bool>> filter)
        {
            return Repository.CountAsync(filter);
        }
        /// <summary>
        /// 异步判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        protected virtual async Task<bool> ExistsCoreAsync(Expression<Func<T, bool>> where, IClientSessionHandle? session)
        {
            var cursor = await Repository.FindAsync(where, session);
            return cursor.FirstOrDefault() != null;
        }
        /// <summary>
        /// 异步判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        protected virtual async Task<bool> ExistsCoreAsync(DynamicFilter where, IClientSessionHandle? session)
        {
            var cursor = await Repository.FindAsync(new DynamicFindOptions<T> { Filter = where }, session);
            return cursor.FirstOrDefault() != null;
        }
        //protected virtual Task AddCoreAsync(T entity, IClientSessionHandle? session)
        //{
        //    FillSystemField(entity, false);
        //    return Repository.InsertAsync(entity, session);
        //}
        /// <summary>
        /// 异步批量新增实体。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual async Task AddCoreAsync(IEnumerable<T> entities, IClientSessionHandle? session)
        {
            entities.ForEach(entity => FillSystemField(entity, false));
            await BeforeAdd(entities, session);
            await Repository.InsertAsync(entities, session);
            CreateAuditLog(DbAction.Insert, null, entities, null, null, session);
            await AfterAdd(entities, session);
            return;
        }
        //protected virtual Task<UpdateResult> PatchCoreAsync(string id, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        //{
        //    update = FillSystemField(update);
        //    return Repository.UpdateAsync(id, update, upsert, session);
        //}
        //protected virtual Task<UpdateResult> PatchManyCoreAsync(DynamicFilter filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        //{
        //    update = FillSystemField(update);
        //    return Repository.UpdateManyAsync(filter, update, upsert, session);
        //}
        /// <summary>
        /// 异步根据过滤定义批量更新实体。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>更新结果。</returns>
        protected virtual async Task<UpdateResult> PatchManyCoreAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        {
            update = FillSystemField(update);
            await BeforeUpdate(filter, update, upsert, session);
            var result = await Repository.UpdateManyAsync(filter, update, upsert, session);
            CreateAuditLog(DbAction.Update, null, null, filter, update, session);
            await AfterUpdate(filter, update, upsert, session);
            return result;
        }
        /// <summary>
        /// 异步替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>替换结果。</returns>
        protected virtual async Task<ReplaceOneResult> ReplaceCoreAsync(T entity, IClientSessionHandle? session)
        {
            var entityId = entity.Id;
            FillSystemField(entity, true);
            await BeforeReplace(entity, session);
            var old = ScopeCache.Get<T>(entityId, DataVersion.Old) ?? await GetCoreAsync(entityId, session);
            var result = await Repository.ReplaceAsync(entity, session);
            CreateAuditLog(DbAction.Update, old == null ? null : [old], [entity], null, null, session);
            await AfterReplace(entity, session);
            return result;
        }
        //protected virtual Task<DeleteResult> DeleteCoreAsync(string id, IClientSessionHandle? session)
        //{
        //    return Repository.DeleteAsync(id, session);
        //}
        //protected virtual Task<DeleteResult> DeleteCoreAsync(IEnumerable<string> ids, IClientSessionHandle? session)
        //{
        //    return Repository.DeleteAsync(ids, session);
        //}
        //protected virtual Task<DeleteResult> DeleteCoreAsync(DynamicFilter filter, IClientSessionHandle? session)
        //{
        //    return Repository.DeleteAsync(filter, session);
        //}
        /// <summary>
        /// 异步根据过滤定义删除实体（支持逻辑删除）。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        /// <returns>删除结果对象。</returns>
        protected virtual async Task<object> DeleteCoreAsync(FilterDefinition<T> filter, IClientSessionHandle? session)
        {
            await BeforeDelete(filter, session);
            if (LogicDelete && IDeleteFlagType.IsAssignableFrom(typeof(T)))
            {
                var update = UpdateBuilder.Set(Fields.DeleteFlag, true);
                var result = await Repository.UpdateManyAsync(filter, update, session: session);
                CreateAuditLog(DbAction.Delete, null, null, filter, null, session);
                await AfterDelete(filter, session);
                return result;
            }
            else
            {
                var result = await Repository.DeleteAsync(filter, session);
                CreateAuditLog(DbAction.Delete, null, null, filter, null, session);
                await AfterDelete(filter, session);
                return result;
            }
        }

        #endregion

        /// <summary>
        /// 根据 Bson 文档构建更新定义。
        /// </summary>
        /// <param name="bson">Bson 文档。</param>
        /// <returns>更新定义。</returns>
        protected virtual UpdateDefinition<T> GetUpdateDefinition(BsonDocument bson)
        {
            var updateList = new List<UpdateDefinition<T>>();
            updateList.AddRange(BuildUpdateDefinition(bson, null));
            return UpdateBuilder.Combine(updateList);
        }
        /// <summary>
        /// 递归构建更新定义列表。
        /// </summary>
        /// <param name="bson">Bson 文档。</param>
        /// <param name="parent">父字段路径前缀。</param>
        /// <returns>更新定义列表。</returns>
        protected List<UpdateDefinition<T>> BuildUpdateDefinition(BsonDocument bson, string? parent)
        {
            var updateList = new List<UpdateDefinition<T>>();
            foreach (var el in bson!.Elements)
            {
                var key = string.IsNullOrEmpty(parent) ? el.Name : $"{parent}.{el.Name}";
                var subUpdateList = new List<UpdateDefinition<T>>();

                if (el.Value.IsBsonDocument)
                {
                    updateList.AddRange(BuildUpdateDefinition(el.Value.ToBsonDocument(), key));
                }
                else if (el.Value.IsBsonArray)
                {
                    var bsonArray = el.Value.AsBsonArray;
                    var i = 0;
                    foreach (var doc in bsonArray)
                    {
                        if (doc.IsBsonDocument)
                        {
                            updateList.AddRange(BuildUpdateDefinition(doc.ToBsonDocument(), $"{key}.{i}"));
                        }
                        else
                        {
                            updateList.Add(UpdateBuilder.Set(key, el.Value));
                            continue;
                        }

                        i++;
                    }
                }
                else
                {
                    updateList.Add(UpdateBuilder.Set(key, el.Value));
                }
            }

            return updateList;
        }

        /// <summary>
        /// 填充系统字段。
        /// </summary>
        /// <param name="entity">要填充的实体。</param>
        /// <param name="isEdit">是否为编辑操作。</param>
        /// <returns>填充后的实体。</returns>
        protected virtual T FillSystemField(T entity, bool isEdit)
        {
            return entity;
        }
        /// <summary>
        /// 填充系统字段。
        /// </summary>
        /// <param name="update">要填充的更新定义。</param>
        /// <returns>填充后的更新定义。</returns>
        protected virtual UpdateDefinition<T> FillSystemField(UpdateDefinition<T> update)
        {
            return update;
        }

        #endregion

        #region Business Core

        /// <summary>
        /// 新增前的钩子方法，子类可重写。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task BeforeAdd(IEnumerable<T> entities, IClientSessionHandle? session) { return Task.CompletedTask; }

        /// <summary>
        /// 新增后的钩子方法，子类可重写。
        /// </summary>
        /// <param name="entities">已新增的实体集合。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task AfterAdd(IEnumerable<T> entities, IClientSessionHandle? session) { return Task.CompletedTask; }

        /// <summary>
        /// 替换前的钩子方法，子类可重写。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task BeforeReplace(T entity, IClientSessionHandle? session) { return Task.CompletedTask; }

        /// <summary>
        /// 替换后的钩子方法，子类可重写。
        /// </summary>
        /// <param name="entity">已替换的实体。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task AfterReplace(T entity, IClientSessionHandle? session) { return Task.CompletedTask; }

        /// <summary>
        /// 更新前的钩子方法，子类可重写。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task BeforeUpdate(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session) { return Task.CompletedTask; }

        /// <summary>
        /// 更新后的钩子方法，子类可重写。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="update">更新定义。</param>
        /// <param name="upsert">不存在时是否插入。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task AfterUpdate(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session) { return Task.CompletedTask; }

        /// <summary>
        /// 删除前的钩子方法，子类可重写。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task BeforeDelete(FilterDefinition<T> filter, IClientSessionHandle? session) { return Task.CompletedTask; }

        /// <summary>
        /// 删除后的钩子方法，子类可重写。
        /// </summary>
        /// <param name="filter">过滤定义。</param>
        /// <param name="session">可选的客户端会话句柄。</param>
        protected virtual Task AfterDelete(FilterDefinition<T> filter, IClientSessionHandle? session) { return Task.CompletedTask; }

        #endregion
    }
}
