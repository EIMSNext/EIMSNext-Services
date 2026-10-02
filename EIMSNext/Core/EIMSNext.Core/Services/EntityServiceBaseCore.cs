using System.Linq.Expressions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using HKH.Mef2.Integration;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 实体服务基类，为 <typeparamref name="T"/> 实体提供 <see cref="IService{T}"/> 的默认实现。
    /// <para>
    /// 这是服务层的最底层实现（仓储读写、事务编排）；带审计字段填充的
    /// <see cref="EntityServiceBase{T}"/> 派生于它。
    /// </para>
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
    public abstract class EntityServiceBaseCore<T> : ServiceCore<T>, IService<T> where T : class, IEntityKey
    {
        /// <summary>
        /// 初始化 <see cref="EntityServiceBaseCore{T}"/> 类的新实例。
        /// </summary>
        public EntityServiceBaseCore(IResolver resolver)
            : base(resolver)
        {
        }

        #region Methods

        /// <summary>
        /// 根据主键 ID 获取实体。
        /// </summary>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        public T? Get(string id) => GetCore(id);

        /// <summary>
        /// 获取全部实体的可查询对象。
        /// </summary>
        public IQueryable<T> All() => Repository.Queryable;

        /// <summary>
        /// 根据表达式过滤条件查询实体。
        /// </summary>
        public IQueryable<T> Query(Expression<Func<T, bool>> where) => Repository.Queryable.Where(where);

        /// <summary>
        /// 根据动态查询选项查找实体。
        /// </summary>
        public IQueryable<T> Find(DynamicFindOptions<T> options) => FindCore(options);

        /// <summary>
        /// 根据表达式过滤条件查找实体。
        /// </summary>
        public IQueryable<T> Find(Expression<Func<T, bool>> filter) => FindCore(filter);

        /// <summary>
        /// 根据动态过滤条件查找实体。
        /// </summary>
        public IQueryable<T> Find(DynamicFilter filter) => FindCore(filter);

        /// <summary>
        /// 根据动态筛选条件查找实体列表。
        /// </summary>
        public List<T> FindList(DynamicFilter filter) => Repository.FindList(filter);

        /// <summary>
        /// 统计满足动态过滤条件的实体数量。
        /// </summary>
        public long Count(DynamicFilter filter) => CountCore(filter);

        /// <summary>
        /// 统计满足表达式过滤条件的实体数量。
        /// </summary>
        public long Count(Expression<Func<T, bool>> filter) => CountCore(filter);

        /// <summary>
        /// 判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <returns>存在时为 true，否则为 false。</returns>
        public bool Exists(Expression<Func<T, bool>> where) => ExistsCore(where);

        /// <summary>
        /// 判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <returns>存在时为 true，否则为 false。</returns>
        public bool Exists(DynamicFilter where) => ExistsCore(where);

        /// <summary>
        /// 新增单个实体。
        /// </summary>
        public virtual void Add(T entity) => Add([entity]);

        /// <summary>
        /// 批量新增实体。
        /// </summary>
        public virtual void Add(IEnumerable<T> entities)
        {
            using var scope = NewTransactionScope();
            AddCore(entities);
            scope.CommitTransaction();
        }

        /// <summary>
        /// 替换（整行更新）单个实体。
        /// </summary>
        public virtual int Replace(T entity) => ReplaceCore(entity);

        /// <summary>
        /// 根据主键 ID 删除实体。
        /// </summary>
        public virtual int Delete(string id) => DeleteCore(x => x.Id == id);

        /// <summary>
        /// 根据多个主键 ID 批量删除实体。
        /// </summary>
        public virtual int Delete(IEnumerable<string> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return 0;

            using var scope = NewTransactionScope();
            var result = DeleteCore(x => idList.Contains(x.Id));
            scope.CommitTransaction();
            return result;
        }

        /// <summary>
        /// 根据动态过滤条件批量删除实体。
        /// </summary>
        public virtual int Delete(DynamicFilter filter)
        {
            using var scope = NewTransactionScope();
            var result = DeleteCore(filter);
            scope.CommitTransaction();
            return result;
        }

        #endregion

        #region Async Methods

        /// <summary>
        /// 异步根据主键 ID 获取实体。
        /// </summary>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        public Task<T?> GetAsync(string id) => GetCoreAsync(id);

        /// <summary>
        /// 异步根据动态查询选项查找实体列表。
        /// </summary>
        public Task<List<T>> FindAsync(DynamicFindOptions<T> options, CancellationToken cancellationToken = default)
            => FindCoreAsync(options, cancellationToken);

        /// <summary>
        /// 异步根据表达式过滤条件查找实体列表。
        /// </summary>
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => FindCoreAsync(filter, cancellationToken);

        /// <summary>
        /// 异步根据动态筛选条件查找实体列表。
        /// </summary>
        public Task<List<T>> FindAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => Repository.FindAsync(filter, cancellationToken);

        /// <summary>
        /// 异步统计满足动态过滤条件的实体数量。
        /// </summary>
        public Task<long> CountAsync(DynamicFilter filter) => CountCoreAsync(filter);

        /// <summary>
        /// 异步统计满足表达式过滤条件的实体数量。
        /// </summary>
        public Task<long> CountAsync(Expression<Func<T, bool>> filter) => CountCoreAsync(filter);

        /// <summary>
        /// 异步判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <returns>存在时为 true，否则为 false。</returns>
        public Task<bool> ExistsAsync(Expression<Func<T, bool>> where) => ExistsCoreAsync(where);

        /// <summary>
        /// 异步判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <returns>存在时为 true，否则为 false。</returns>
        public Task<bool> ExistsAsync(DynamicFilter where) => ExistsCoreAsync(where);

        /// <summary>
        /// 异步新增单个实体。
        /// </summary>
        public virtual Task AddAsync(T entity) => AddAsync([entity]);

        /// <summary>
        /// 异步批量新增实体。
        /// </summary>
        public virtual async Task AddAsync(IEnumerable<T> entities)
        {
            await using var scope = NewTransactionScope();
            await AddCoreAsync(entities).ConfigureAwait(false);
            await scope.CommitTransactionAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 异步替换（整行更新）单个实体。
        /// </summary>
        public virtual Task<int> ReplaceAsync(T entity) => ReplaceCoreAsync(entity);

        /// <summary>
        /// 异步根据主键 ID 删除实体。
        /// </summary>
        public virtual Task<int> DeleteAsync(string id) => DeleteCoreAsync(x => x.Id == id);

        /// <summary>
        /// 异步根据多个主键 ID 批量删除实体。
        /// </summary>
        public virtual async Task<int> DeleteAsync(IEnumerable<string> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return 0;

            await using var scope = NewTransactionScope();
            var result = await DeleteCoreAsync(x => idList.Contains(x.Id)).ConfigureAwait(false);
            await scope.CommitTransactionAsync().ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// 异步根据动态过滤条件批量删除实体。
        /// </summary>
        public virtual async Task<int> DeleteAsync(DynamicFilter filter)
        {
            await using var scope = NewTransactionScope();
            var result = await DeleteCoreAsync(filter).ConfigureAwait(false);
            await scope.CommitTransactionAsync().ConfigureAwait(false);
            return result;
        }

        #endregion
    }
}
