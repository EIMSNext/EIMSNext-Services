using System.Linq.Expressions;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace EIMSNext.Core.Repositories
{
    /// <summary>
    /// 仓储实现的公共基类：承载 EF Core 上下文与最基础的主键/查询语义。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
    public abstract class RepositoryBase<T> : IRepository<T> where T : class, IEntityKey
    {
        /// <summary>
        /// 初始化 <see cref="RepositoryBase{T}"/> 类的新实例。
        /// </summary>
        protected RepositoryBase(DbContext context) => Context = context;

        /// <summary>
        /// 获取数据库上下文。
        /// </summary>
        protected DbContext Context { get; }

        /// <inheritdoc />
        public DbContext DbContext => Context;

        /// <summary>
        /// 实体集合。默认 AsNoTracking：绝大多数读取只用于投影返回，跟踪实体既浪费内存，
        /// 也会让批处理场景的变更跟踪开销随行数线性增长。需要更新时由 Update/Replace 显式附加。
        /// </summary>
        public IQueryable<T> Queryable => Context.Set<T>().AsNoTracking();

        /// <inheritdoc />
        public T? Get(string id) => Queryable.FirstOrDefault(x => x.Id == id);

        /// <inheritdoc />
        public Task<T?> GetAsync(string id, CancellationToken cancellationToken = default)
            => Queryable.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        /// <inheritdoc />
        public long Count(Expression<Func<T, bool>> predicate) => Queryable.LongCount(predicate);

        /// <inheritdoc />
        public Task<long> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Queryable.LongCountAsync(predicate, cancellationToken);

        /// <inheritdoc />
        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Queryable.AnyAsync(predicate, cancellationToken);

        /// <summary>
        /// 主键生成。Id 保持 string 契约，产出 <see cref="TsidIdGenerator"/> 的 TSID 字符串。
        /// </summary>
        public string NewId() => TsidIdGenerator.NewId();

        /// <inheritdoc />
        public T EnsureId(T entity)
        {
            if (string.IsNullOrEmpty(entity.Id)) entity.Id = NewId();
            return entity;
        }

        /// <inheritdoc />
        public IEnumerable<T> EnsureId(IEnumerable<T> entities)
        {
            foreach (var entity in entities) EnsureId(entity);
            return entities;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 用底层上下文开启环境事务，嵌套调用复用最外层事务。
        /// </remarks>
        public TransactionScope NewTransactionScope() => new(Context);

        #region 由子类实现

        /// <inheritdoc />
        public abstract IQueryable<T> Find(Query.QueryFindOptions<T> options);

        /// <inheritdoc />
        public abstract IQueryable<T> Find(Expression<Func<T, bool>> filter);

        /// <inheritdoc />
        public abstract Task<List<T>> FindAsync(Query.QueryFindOptions<T> options, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract void Insert(T entity);

        /// <inheritdoc />
        public abstract void Insert(IEnumerable<T> entities);

        /// <inheritdoc />
        public abstract Task InsertAsync(T entity, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract void Replace(T entity);

        /// <inheritdoc />
        public abstract Task ReplaceAsync(T entity, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task<int> UpdateAsync(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task<int> UpdateAsync(
            string id,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract int UpdateMany(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters);

        /// <inheritdoc />
        public abstract Task<int> UpdateManyAsync(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract void Delete(T entity);

        /// <inheritdoc />
        public abstract int Delete(string id);

        /// <inheritdoc />
        public abstract int Delete(IEnumerable<string> ids);

        /// <inheritdoc />
        public abstract Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task<int> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task<int> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task<int> SoftDeleteManyAsync(
            IEnumerable<string> ids,
            Operator? deleteBy,
            long? deleteTime,
            CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task<int> SoftDeleteManyAsync(
            Expression<Func<T, bool>> predicate,
            Operator? deleteBy,
            long? deleteTime,
            CancellationToken cancellationToken = default);

        #endregion
    }
}
