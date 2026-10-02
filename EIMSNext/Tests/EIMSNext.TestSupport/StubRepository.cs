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
    /// 全成员声明为 <c>virtual</c>：测试里的内存假仓储（如仅需要 <see cref="Queryable"/>
    /// 的 Webhook 假仓储）可以直接继承本桩并只覆写真正被用到的一两个成员，
    /// 避免每个测试文件重复实现几十个 <c>throw new NotSupportedException()</c>。
    /// </para>
    /// </summary>
    public class StubRepository<T> : IRepository<T> where T : class, IEntityKey
    {
        #region 查询

        public virtual IQueryable<T> Queryable => throw new NotSupportedException();

        public virtual T? Get(string id) => throw new NotSupportedException();

        public virtual Task<T?> GetAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual IQueryable<T> Find(QueryFindOptions<T> options) => throw new NotSupportedException();

        public virtual IQueryable<T> Find(Expression<Func<T, bool>> filter) => throw new NotSupportedException();

        public virtual Task<List<T>> FindAsync(QueryFindOptions<T> options, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual long Count(Expression<Func<T, bool>> predicate) => throw new NotSupportedException();

        public virtual Task<long> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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

        public virtual Task<int> UpdateManyAsync(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual void Delete(T entity) => throw new NotSupportedException();

        public virtual int Delete(string id) => throw new NotSupportedException();

        public virtual int Delete(IEnumerable<string> ids) => throw new NotSupportedException();

        public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<int> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<int> SoftDeleteManyAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public virtual Task<int> SoftDeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
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
        /// 它只登记「无事务」的上下文标记，不碰数据库。因为没有 <c>DbContext</c>，
        /// 提交时的落库会被跳过（<c>_dbContext</c> 为 null），正是内存桩需要的空操作语义。
        /// 这样被测代码里 <c>using var scope = Repository.NewTransactionScope()</c> 这类写法可以直接跑通，
        /// 又不会让内存桩假装拥有真实事务语义。
        /// </remarks>
        public virtual TransactionScope NewTransactionScope() => new(null!, enabled: false);

        #endregion
    }
}
