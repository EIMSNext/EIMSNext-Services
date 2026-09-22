using System.Linq.Expressions;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace EIMSNext.TestSupport
{
    /// <summary>
    /// 最小仓储桩。
    /// <para>
    /// 迁移说明：原桩实现的是 Mongo 时代的 <c>IRepository&lt;T&gt;</c>
    /// （<c>IMongoCollection</c> / <c>FilterDefinition</c> / <c>IClientSessionHandle</c> /
    /// <c>MongoFindOptions</c> 等）。PostgreSQL 迁移后仓储接口完全基于 EF Core，
    /// 本桩只需实现新的 <see cref="IRepository{T}"/> 成员，并且和以前一样——
    /// 只要不被真正调用，一律 <see cref="NotSupportedException"/>。
    /// </para>
    /// <para>
    /// 全成员声明为 <c>virtual</c>：测试里的内存假仓储（如仅需要 <see cref="Queryable"/>
    /// 的 Webhook 假仓储）可以直接继承本桩并只覆写真正被用到的一两个成员，
    /// 避免每个测试文件重复实现几十个 <c>throw new NotSupportedException()</c>。
    /// </para>
    /// </summary>
    /// <typeparam name="T">实体类型。</typeparam>
    public class StubRepository<T> : IRepository<T> where T : class, IEntityKey
    {
        #region 查询

        public virtual IQueryable<T> Queryable => throw new NotSupportedException();

        public virtual T? Get(string id) => throw new NotSupportedException();

        public virtual Task<T?> GetAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual IQueryable<T> Find(QueryFindOptions<T> options) => throw new NotSupportedException();

        public virtual IQueryable<T> Find(DynamicFindOptions<T> options) => throw new NotSupportedException();

        public virtual IQueryable<T> Find(Expression<Func<T, bool>> filter) => throw new NotSupportedException();

        public virtual IQueryable<T> Find(DynamicFilter filter) => throw new NotSupportedException();

        public virtual Task<List<T>> FindAsync(QueryFindOptions<T> options, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<List<T>> FindAsync(DynamicFindOptions<T> options, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<List<T>> FindAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual List<T> FindList(DynamicFilter filter) => throw new NotSupportedException();

        public virtual Task<List<object?>> DistinctFieldValuesAsync(
            DynamicFilter? filter,
            string fieldPath,
            int limit = 0,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual long Count(Expression<Func<T, bool>> predicate) => throw new NotSupportedException();

        public virtual Task<long> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual long Count(DynamicFilter filter) => throw new NotSupportedException();

        public virtual Task<long> CountAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<bool> AnyAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual IQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector) => throw new NotSupportedException();

        public virtual IQueryable<T> Page<TKey>(Expression<Func<T, TKey>> orderBy, int skip, int take) => throw new NotSupportedException();

        #endregion

        #region 写入

        public virtual void Insert(T entity) => throw new NotSupportedException();

        public virtual void Insert(IEnumerable<T> entities) => throw new NotSupportedException();

        public virtual Task InsertAsync(T entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual void Replace(T entity) => throw new NotSupportedException();

        public virtual Task ReplaceAsync(T entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> UpdateAsync(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> UpdateAsync(
            string id,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual int UpdateMany(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters) => throw new NotSupportedException();

        public virtual int UpdateMany(
            DynamicFilter filter,
            Action<UpdateSettersBuilder<T>> setters) => throw new NotSupportedException();

        public virtual int Update(
            string id,
            Action<UpdateSettersBuilder<T>> setters) => throw new NotSupportedException();

        public virtual Task<int> UpdateManyAsync(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> UpdateManyAsync(
            DynamicFilter filter,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual void Delete(T entity) => throw new NotSupportedException();

        public virtual int Delete(string id) => throw new NotSupportedException();

        public virtual int Delete(IEnumerable<string> ids) => throw new NotSupportedException();

        public virtual int Delete(DynamicFilter filter) => throw new NotSupportedException();

        public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> DeleteAsync(DynamicFilter filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<int> DeleteManyAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<int> SoftDeleteManyAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<int> SoftDeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<int> SoftDeleteManyAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        #endregion

        #region 其它

        public virtual DbContext DbContext => throw new NotSupportedException();

        public virtual string NewId() => throw new NotSupportedException();

        public virtual T EnsureId(T entity) => throw new NotSupportedException();

        public virtual IEnumerable<T> EnsureId(IEnumerable<T> entities) => throw new NotSupportedException();

        /// <summary>
        /// 仓储级事务作用域。
        /// </summary>
        /// <remarks>
        /// 内存桩没有真实的 <see cref="DbContext"/>，因此默认返回一个<b>未启用</b>的作用域：
        /// 它只登记「无事务」的上下文标记，不碰数据库，提交/释放都直接短路
        /// （<see cref="TransactionScope.CommitTransactionAsync"/>、<c>DisposeAsync</c> 在
        /// <c>_ownTransaction</c> 为 null 时均为空操作）。
        /// 这样被测代码里 <c>using var scope = Repository.NewTransactionScope()</c> 这类写法可以直接跑通，
        /// 又不会让内存桩假装拥有真实事务语义。
        /// </remarks>
        public virtual TransactionScope NewTransactionScope() => new(null!, enabled: false);

        public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        #endregion
    }
}
