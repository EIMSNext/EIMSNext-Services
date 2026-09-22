using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 服务基类，提供基于 EF Core 仓储的通用查询与增删改操作、审计日志、缓存及业务钩子。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
    /// <remarks>
    /// <para>
    /// PostgreSQL 迁移要点：
    /// <list type="number">
    /// <item><description>原有的 6 个 Mongo 定义构建器（Filter/Sort/Search/Projection/Update）已移除。
    /// 过滤条件改为表达式树，由 <see cref="IRepository{T}.Find(Expression{Func{T, bool}})"/> 承载；
    /// 动态条件走 <see cref="IRepository{T}.Find(QueryFindOptions{T})"/>。</description></item>
    /// <item><description><c>session</c> 参数全部去除。事务由仓储在写方法内部按需借用环境事务
    /// （见 <c>DbRepository</c>），需要跨多次写入原子提交时由 <see cref="TransactionScope"/>
    /// 建立外层事务。</description></item>
    /// <item><description>Mongo 的 <c>ReplaceOneResult</c>/<c>UpdateResult</c>/<c>DeleteResult</c>
    /// 统一替换为 <see cref="int"/> 受影响行数，语义更直白且不引入驱动类型。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public abstract class ServiceCore<T> where T : class, IEntityKey
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
        /// 获取数据库上下文，用于建立事务作用域。
        /// </summary>
        protected DbContext DbContext => Repository.DbContext;

        #endregion

        #region Helper

        /// <summary>
        /// 创建一个新的事务作用域。
        /// </summary>
        /// <returns>新的事务作用域实例。</returns>
        /// <remarks>
        /// PostgreSQL 不支持真正的嵌套事务，因此内层作用域会复用外层事务，
        /// 只有最外层负责提交与回滚。
        /// </remarks>
        protected TransactionScope NewTransactionScope()
        {
            return new TransactionScope(DbContext, TransNeeded);
        }

        /// <summary>
        /// 在事务中执行操作，并对瞬态冲突（序列化失败、死锁）自动重试。
        /// 已处于事务中时直接执行，不重复开启。
        /// </summary>
        /// <typeparam name="TResult">结果类型。</typeparam>
        /// <param name="operation">操作。</param>
        /// <param name="maxRetries">最大重试次数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>操作结果。</returns>
        protected Task<TResult> ExecuteWithTransactionRetryAsync<TResult>(
            Func<Task<TResult>> operation,
            int? maxRetries = null,
            CancellationToken cancellationToken = default)
            => TransactionScope.ExecuteWithRetryAsync(DbContext, operation, maxRetries ?? 3, cancellationToken);

        /// <summary>
        /// 在事务中执行操作（无返回值），并对瞬态冲突自动重试。
        /// </summary>
        /// <param name="operation">操作。</param>
        /// <param name="maxRetries">最大重试次数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        protected Task ExecuteWithTransactionRetryAsync(
            Func<Task> operation,
            int? maxRetries = null,
            CancellationToken cancellationToken = default)
            => TransactionScope.ExecuteWithRetryAsync(DbContext, operation, maxRetries ?? 3, cancellationToken);

        /// <summary>
        /// 记录审计日志。
        /// </summary>
        /// <param name="action">数据库操作类型。</param>
        /// <param name="oldData">变更前的实体集合。</param>
        /// <param name="newData">变更后的实体集合。</param>
        /// <param name="dataFilter">操作对应的过滤条件描述。</param>
        /// <param name="update">操作对应的更新描述。</param>
        protected virtual void CreateAuditLog(DbAction action, IEnumerable<T>? oldData, IEnumerable<T>? newData, string? dataFilter = null, string? update = null)
        {
            if (!LogAudit) return;

            var logList = new List<AuditLog>();
            if (action == DbAction.Insert && newData != null)
            {
                logList = CreateInsertLog(newData);
            }
            else if (action == DbAction.Update)
            {
                logList = CreateUpdateLog(oldData, newData, dataFilter, update);
            }
            else if (action == DbAction.Delete)
            {
                logList = CreateDeleteLog(oldData, dataFilter);
            }

            if (logList.Count == 0) return;

            if (TransactionScope.IsInTransaction)
            {
                // TODO: 后续改为分布式审计队列，确保审计失败可重试且不阻塞业务事务。
                TransactionScope.RegisterAfterCommitAsync(DbContext, async () =>
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
                AuditLogRepository.InsertAsync(logList).GetAwaiter().GetResult();
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
                Detail = "新增数据:", //TODO:考虑显示一两个主字段？
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
        /// <param name="dataFilter">更新对应的过滤条件描述。</param>
        /// <param name="update">更新描述。</param>
        /// <returns>更新审计日志列表。</returns>
        protected virtual List<AuditLog> CreateUpdateLog(IEnumerable<T>? oldData, IEnumerable<T>? newData, string? dataFilter = null, string? update = null)
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
                    Detail = $"批量更新数据(无旧对象):{dataFilter}",
                    DataFilter = dataFilter,
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
        /// <param name="dataFilter">删除对应的过滤条件描述。</param>
        /// <returns>删除审计日志列表。</returns>
        protected virtual List<AuditLog> CreateDeleteLog(IEnumerable<T>? oldData, string? dataFilter = null)
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
                    Detail = "批量删除数据:",
                    DataFilter = dataFilter,
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
                    Detail = "删除数据:", //TODO:考虑显示一两个主字段？
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
        protected virtual S? GetFromStore<S>(string key, DataVersion version = DataVersion.Temp) where S : class, IEntityKey
        {
            return ScopeCache.Get<S>(key, version, id => Resolver.GetRepository<S>().Get(id));
        }

        #endregion

        #region Methods

        /// <summary>
        /// 根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        protected virtual T? GetCore(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Repository.Get(id);
        }

        /// <summary>
        /// 按过滤谓词查询实体。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        /// <returns>可继续链式操作的查询。</returns>
        protected virtual IQueryable<T> FindCore(Expression<Func<T, bool>> filter)
        {
            return Repository.Find(filter);
        }

        /// <summary>
        /// 按动态过滤条件查询实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>可进一步链式操作的查询。</returns>
        protected virtual IQueryable<T> FindCore(DynamicFilter filter)
        {
            return Repository.Find(filter);
        }

        /// <summary>
        /// 按动态查询选项查询实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <returns>可继续链式操作的查询。</returns>
        protected virtual IQueryable<T> FindCore(DynamicFindOptions<T> options)
        {
            return Repository.Find(options.ToQueryFindOptions<T>());
        }

        /// <summary>
        /// 统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        protected virtual long CountCore(DynamicFilter filter) => Repository.Count(filter);

        /// <summary>
        /// 统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        protected virtual long CountCore(Expression<Func<T, bool>> filter) => Repository.Count(filter);

        /// <summary>
        /// 判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        protected virtual bool ExistsCore(Expression<Func<T, bool>> where) => Repository.Find(where).Any();

        /// <summary>
        /// 判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        protected virtual bool ExistsCore(DynamicFilter where) => Repository.FindList(where).Count > 0;

        /// <summary>
        /// 批量新增实体。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        protected virtual void AddCore(IEnumerable<T> entities)
        {
            var list = entities.ToList();
            list.ForEach(entity => FillSystemField(entity, false));
            BeforeAdd(list).GetAwaiter().GetResult();
            Repository.InsertAsync(list).GetAwaiter().GetResult();
            CreateAuditLog(DbAction.Insert, null, list);
            AfterAdd(list).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <returns>受影响行数。</returns>
        protected virtual int ReplaceCore(T entity)
        {
            var entityId = entity.Id;
            FillSystemField(entity, true);
            BeforeReplace(entity).GetAwaiter().GetResult();
            var old = ScopeCache.Get<T>(entityId, DataVersion.Old) ?? GetCore(entityId);
            Repository.Replace(entity);
            CreateAuditLog(DbAction.Update, old == null ? null : [old], [entity]);
            AfterReplace(entity).GetAwaiter().GetResult();
            return 1;
        }

        /// <summary>
        /// 按过滤谓词批量更新实体。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        /// <param name="setters">字段更新表达式。</param>
        /// <returns>受影响行数。</returns>
        protected virtual int PatchManyCore(
            Expression<Func<T, bool>> filter,
            Action<UpdateSettersBuilder<T>> setters)
        {
            BeforeUpdate(filter, setters).GetAwaiter().GetResult();
            var result = Repository.UpdateManyAsync(filter, setters).GetAwaiter().GetResult();
            CreateAuditLog(DbAction.Update, null, null, filter.ToString());
            AfterUpdate(filter, setters).GetAwaiter().GetResult();
            return result;
        }

        /// <summary>
        /// 根据过滤谓词删除实体（支持逻辑删除）。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        /// <returns>受影响行数。</returns>
        protected virtual int DeleteCore(Expression<Func<T, bool>> filter)
        {
            BeforeDelete(filter).GetAwaiter().GetResult();
            int result;
            if (LogicDelete && IDeleteFlagType.IsAssignableFrom(typeof(T)))
            {
                result = Repository.SoftDeleteManyAsync(filter).GetAwaiter().GetResult();
            }
            else
            {
                result = Repository.DeleteManyAsync(filter).GetAwaiter().GetResult();
            }

            CreateAuditLog(DbAction.Delete, null, null, filter.ToString());
            AfterDelete(filter).GetAwaiter().GetResult();
            return result;
        }

        /// <summary>
        /// 根据动态过滤条件删除实体（支持逻辑删除）。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>受影响行数。</returns>
        protected virtual int DeleteCore(DynamicFilter filter)
        {
            var predicate = filter.ToPredicate<T>();
            BeforeDelete(predicate).GetAwaiter().GetResult();
            int result;
            if (LogicDelete && IDeleteFlagType.IsAssignableFrom(typeof(T)))
            {
                result = Repository.SoftDeleteManyAsync(predicate).GetAwaiter().GetResult();
            }
            else
            {
                result = Repository.DeleteManyAsync(predicate).GetAwaiter().GetResult();
            }

            CreateAuditLog(DbAction.Delete, null, null, filter.ToString());
            AfterDelete(predicate).GetAwaiter().GetResult();
            return result;
        }

        private static string GetChangeDetail(T oldT, T newT)
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

        /// <summary>
        /// 填充系统字段（实体）。
        /// </summary>
        /// <param name="entity">要填充的实体。</param>
        /// <param name="isEdit">是否为编辑操作。</param>
        /// <returns>填充后的实体。</returns>
        protected virtual T FillSystemField(T entity, bool isEdit)
        {
            return entity;
        }

        #endregion

        #region Async Methods

        /// <summary>
        /// 异步根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        protected virtual Task<T?> GetCoreAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return Task.FromResult<T?>(null);
            return Repository.GetAsync(id);
        }

        /// <summary>
        /// 异步按过滤谓词查询实体列表。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>实体列表。</returns>
        protected virtual Task<List<T>> FindCoreAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => Repository.FindAsync(filter, cancellationToken);

        /// <summary>
        /// 异步按动态查询选项查询实体列表。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>实体列表。</returns>
        protected virtual Task<List<T>> FindCoreAsync(DynamicFindOptions<T> options, CancellationToken cancellationToken = default)
            => Repository.FindAsync(options.ToQueryFindOptions<T>(), cancellationToken);

        /// <summary>
        /// 异步统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        protected virtual Task<long> CountCoreAsync(DynamicFilter filter) => Repository.CountAsync(filter);

        /// <summary>
        /// 异步统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        protected virtual Task<long> CountCoreAsync(Expression<Func<T, bool>> filter) => Repository.CountAsync(filter);

        /// <summary>
        /// 异步判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        protected virtual Task<bool> ExistsCoreAsync(Expression<Func<T, bool>> where, CancellationToken cancellationToken = default)
            => Repository.AnyAsync(where, cancellationToken);

        /// <summary>
        /// 异步判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        protected virtual Task<bool> ExistsCoreAsync(DynamicFilter where, CancellationToken cancellationToken = default)
            => Repository.AnyAsync(where, cancellationToken);

        /// <summary>
        /// 异步批量新增实体。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        protected virtual async Task AddCoreAsync(IEnumerable<T> entities)
        {
            var list = entities.ToList();
            list.ForEach(entity => FillSystemField(entity, false));
            await BeforeAdd(list).ConfigureAwait(false);
            await Repository.InsertAsync(list).ConfigureAwait(false);
            CreateAuditLog(DbAction.Insert, null, list);
            await AfterAdd(list).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <returns>受影响行数。</returns>
        protected virtual async Task<int> ReplaceCoreAsync(T entity)
        {
            var entityId = entity.Id;
            FillSystemField(entity, true);
            await BeforeReplace(entity).ConfigureAwait(false);
            var old = ScopeCache.Get<T>(entityId, DataVersion.Old) ?? await GetCoreAsync(entityId).ConfigureAwait(false);
            await Repository.ReplaceAsync(entity).ConfigureAwait(false);
            CreateAuditLog(DbAction.Update, old == null ? null : [old], [entity]);
            await AfterReplace(entity).ConfigureAwait(false);
            return 1;
        }

        /// <summary>
        /// 异步按过滤谓词批量更新实体。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        /// <param name="setters">字段更新表达式。</param>
        /// <returns>受影响行数。</returns>
        protected virtual async Task<int> PatchManyCoreAsync(
            Expression<Func<T, bool>> filter,
            Action<UpdateSettersBuilder<T>> setters)
        {
            await BeforeUpdate(filter, setters).ConfigureAwait(false);
            var result = await Repository.UpdateManyAsync(filter, setters).ConfigureAwait(false);
            CreateAuditLog(DbAction.Update, null, null, filter.ToString());
            await AfterUpdate(filter, setters).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// 异步根据过滤谓词删除实体（支持逻辑删除）。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        /// <returns>受影响行数。</returns>
        protected virtual async Task<int> DeleteCoreAsync(Expression<Func<T, bool>> filter)
        {
            await BeforeDelete(filter).ConfigureAwait(false);
            int result;
            if (LogicDelete && IDeleteFlagType.IsAssignableFrom(typeof(T)))
            {
                result = await Repository.SoftDeleteManyAsync(filter).ConfigureAwait(false);
            }
            else
            {
                result = await Repository.DeleteManyAsync(filter).ConfigureAwait(false);
            }

            CreateAuditLog(DbAction.Delete, null, null, filter.ToString());
            await AfterDelete(filter).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// 异步根据动态过滤条件删除实体（支持逻辑删除）。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>受影响行数。</returns>
        protected virtual async Task<int> DeleteCoreAsync(DynamicFilter filter)
        {
            var predicate = filter.ToPredicate<T>();
            await BeforeDelete(predicate).ConfigureAwait(false);
            int result;
            if (LogicDelete && IDeleteFlagType.IsAssignableFrom(typeof(T)))
            {
                result = await Repository.SoftDeleteManyAsync(predicate).ConfigureAwait(false);
            }
            else
            {
                result = await Repository.DeleteManyAsync(predicate).ConfigureAwait(false);
            }

            CreateAuditLog(DbAction.Delete, null, null, filter.ToString());
            await AfterDelete(predicate).ConfigureAwait(false);
            return result;
        }

        #endregion

        #region Business Core

        /// <summary>
        /// 新增前的钩子方法，子类可重写。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        protected virtual Task BeforeAdd(IEnumerable<T> entities) { return Task.CompletedTask; }

        /// <summary>
        /// 新增后的钩子方法，子类可重写。
        /// </summary>
        /// <param name="entities">已新增的实体集合。</param>
        protected virtual Task AfterAdd(IEnumerable<T> entities) { return Task.CompletedTask; }

        /// <summary>
        /// 替换前的钩子方法，子类可重写。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        protected virtual Task BeforeReplace(T entity) { return Task.CompletedTask; }

        /// <summary>
        /// 替换后的钩子方法，子类可重写。
        /// </summary>
        /// <param name="entity">已替换的实体。</param>
        protected virtual Task AfterReplace(T entity) { return Task.CompletedTask; }

        /// <summary>
        /// 更新前的钩子方法，子类可重写。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        /// <param name="setters">字段更新表达式。</param>
        protected virtual Task BeforeUpdate(
            Expression<Func<T, bool>> filter,
            Action<UpdateSettersBuilder<T>> setters) { return Task.CompletedTask; }

        /// <summary>
        /// 更新后的钩子方法，子类可重写。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        /// <param name="setters">字段更新表达式。</param>
        protected virtual Task AfterUpdate(
            Expression<Func<T, bool>> filter,
            Action<UpdateSettersBuilder<T>> setters) { return Task.CompletedTask; }

        /// <summary>
        /// 删除前的钩子方法，子类可重写。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        protected virtual Task BeforeDelete(Expression<Func<T, bool>> filter) { return Task.CompletedTask; }

        /// <summary>
        /// 删除后的钩子方法，子类可重写。
        /// </summary>
        /// <param name="filter">过滤谓词。</param>
        protected virtual Task AfterDelete(Expression<Func<T, bool>> filter) { return Task.CompletedTask; }

        #endregion
    }
}
