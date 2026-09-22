using System.Linq.Expressions;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;

namespace EIMSNext.TestSupport
{
    /// <summary>
    /// 最小实体服务桩，实现 PostgreSQL 迁移后的 <see cref="IService{T}"/>。
    /// <para>
    /// 迁移说明：Mongo 时代 <c>IService&lt;T&gt;</c> 暴露 <c>IMongoCollection&lt;T&gt; Collection</c>、
    /// <c>IFindFluent&lt;T,T&gt;</c>、<c>IAsyncCursor&lt;T&gt;</c>、<c>ReplaceOneResult</c> 与
    /// <c>object</c> 删除结果；迁移后统一为 <see cref="IQueryable{T}"/>、<c>List&lt;T&gt;</c>
    /// 与 <c>int</c> 受影响行数。本桩提供基于内存列表的默认实现，
    /// 测试只需覆写真正关心的一两个成员。
    /// </para>
    /// <para>
    /// 所有成员声明为 <c>virtual</c>，便于派生类按需覆写（例如只读的内存假服务）。
    /// </para>
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
    public class StubEntityService<T> : IService<T> where T : class, IEntityKey
    {
        private readonly List<T> _items;

        /// <summary>
        /// 初始化 <see cref="StubEntityService{T}"/> 类的新实例。
        /// </summary>
        /// <param name="items">作为后备数据集的内存实体集合。</param>
        public StubEntityService(IEnumerable<T>? items = null)
        {
            _items = items is null ? [] : [.. items];
        }

        /// <summary>
        /// 获取后备数据集。
        /// </summary>
        protected IReadOnlyList<T> Items => _items;

        #region 查询

        public virtual T? Get(string id) => _items.FirstOrDefault(x => x.Id == id);

        public virtual IQueryable<T> All() => _items.AsQueryable();

        public virtual IQueryable<T> Query(Expression<Func<T, bool>> where) => All().Where(where);

        public virtual IQueryable<T> Find(DynamicFindOptions<T> options) => throw new NotSupportedException();

        public virtual IQueryable<T> Find(Expression<Func<T, bool>> filter) => Query(filter);

        public virtual IQueryable<T> Find(DynamicFilter filter) => throw new NotSupportedException();

        public virtual List<T> FindList(DynamicFilter filter) => throw new NotSupportedException();

        public virtual long Count(DynamicFilter filter) => throw new NotSupportedException();

        public virtual long Count(Expression<Func<T, bool>> filter) => All().LongCount(filter);

        public virtual bool Exists(Expression<Func<T, bool>> where) => All().Any(where);

        public virtual bool Exists(DynamicFilter where) => throw new NotSupportedException();

        #endregion

        #region 写入

        public virtual void Add(T entity) => _items.Add(entity);

        public virtual void Add(IEnumerable<T> entities) => _items.AddRange(entities);

        public virtual int Replace(T entity)
        {
            var index = _items.FindIndex(x => x.Id == entity.Id);
            if (index < 0)
            {
                return 0;
            }

            _items[index] = entity;
            return 1;
        }

        public virtual int Delete(string id) => _items.RemoveAll(x => x.Id == id);

        public virtual int Delete(IEnumerable<string> ids)
        {
            var set = ids as ISet<string> ?? new HashSet<string>(ids, StringComparer.Ordinal);
            return _items.RemoveAll(x => set.Contains(x.Id));
        }

        public virtual int Delete(DynamicFilter filter) => throw new NotSupportedException();

        #endregion

        #region 异步

        public virtual Task<T?> GetAsync(string id) => Task.FromResult(Get(id));

        public virtual Task<List<T>> FindAsync(DynamicFindOptions<T> options, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => Task.FromResult(Query(filter).ToList());

        public virtual Task<List<T>> FindAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<long> CountAsync(DynamicFilter filter) => throw new NotSupportedException();

        public virtual Task<long> CountAsync(Expression<Func<T, bool>> filter) => Task.FromResult(Count(filter));

        public virtual Task<bool> ExistsAsync(Expression<Func<T, bool>> where) => Task.FromResult(Exists(where));

        public virtual Task<bool> ExistsAsync(DynamicFilter where) => throw new NotSupportedException();

        public virtual Task AddAsync(T entity)
        {
            Add(entity);
            return Task.CompletedTask;
        }

        public virtual Task AddAsync(IEnumerable<T> entities)
        {
            Add(entities);
            return Task.CompletedTask;
        }

        public virtual Task<int> ReplaceAsync(T entity) => Task.FromResult(Replace(entity));

        public virtual Task<int> DeleteAsync(string id) => Task.FromResult(Delete(id));

        public virtual Task<int> DeleteAsync(IEnumerable<string> ids) => Task.FromResult(Delete(ids));

        public virtual Task<int> DeleteAsync(DynamicFilter filter) => throw new NotSupportedException();

        #endregion
    }
}
