using System.Linq.Expressions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace EIMSNext.Core.Repositories
{
    /// <summary>
    /// <typeparamref name="T"/> 实体的默认仓储实现，基于 EF Core。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
    /// <remarks>
    /// </remarks>
    public class DbRepository<T> : RepositoryBase<T> where T : class, IEntityKey
    {
        /// <summary>
        /// 初始化 <see cref="DbRepository{T}"/> 类的新实例。
        /// </summary>
        public DbRepository(DbContext dbContext)
            : base(dbContext)
        {
        }

        #region 查询

        /// <inheritdoc />
        public override IQueryable<T> Find(QueryFindOptions<T> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            var source = options.IncludeDeleted ? Queryable.IgnoreQueryFilters() : Queryable;
            return Apply(options, source);
        }

        /// <inheritdoc />
        public override IQueryable<T> Find(DynamicFindOptions<T> options)
            => Find(options.ToQueryFindOptions<T>());

        /// <inheritdoc />
        public override IQueryable<T> Find(Expression<Func<T, bool>> filter)
            => filter is null ? Queryable : Queryable.Where(filter);

        /// <inheritdoc />
        public override IQueryable<T> Find(DynamicFilter filter)
            => Find(filter.ToPredicate<T>());

        /// <inheritdoc />
        public override Task<List<T>> FindAsync(QueryFindOptions<T> options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            var source = options.IncludeDeleted ? Queryable.IgnoreQueryFilters() : Queryable;
            return Apply(options, source).ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override Task<List<T>> FindAsync(DynamicFindOptions<T> options, CancellationToken cancellationToken = default)
            => FindAsync(options.ToQueryFindOptions<T>(), cancellationToken);

        /// <inheritdoc />
        public override Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
            => Find(filter).ToListAsync(cancellationToken);

        /// <inheritdoc />
        public override Task<List<T>> FindAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => Find(new QueryFindOptions<T>(filter.ToPredicate<T>())).ToListAsync(cancellationToken);

        /// <inheritdoc />
        public override List<T> FindList(DynamicFilter filter)
            => Find(new QueryFindOptions<T>(filter.ToPredicate<T>())).ToList();

        /// <inheritdoc />
        public override long Count(DynamicFilter filter) => Queryable.LongCount(filter.ToPredicate<T>());

        /// <inheritdoc />
        public override Task<long> CountAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => Queryable.LongCountAsync(filter.ToPredicate<T>(), cancellationToken);

        /// <inheritdoc />
        public override Task<bool> AnyAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => Queryable.AnyAsync(filter.ToPredicate<T>(), cancellationToken);

        /// <inheritdoc />
        public override IQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector)
            => Queryable.Select(selector);

        /// <inheritdoc />
        public override IQueryable<T> Page<TKey>(Expression<Func<T, TKey>> orderBy, int skip, int take)
            => Queryable.OrderBy(orderBy).Skip(Math.Max(0, skip)).Take(take <= 0 ? 200 : take);

        /// <summary>
        /// 取某动态字段的去重值，供筛选选项下拉使用。
        /// </summary>
        /// <remarks>
        /// PostgreSQL 下 <c>Data</c> 是 jsonb，无法用 <c>SELECT DISTINCT</c> 直接取内部键，
        /// 因此这里在物化后于内存里去重。调用方（筛选选项）本身就带 limit，
        /// 且表单数据量受企业维度约束，这个代价可接受；
        /// 若后续成为瓶颈，应改为对 jsonb 键建表达式索引 + 原生 SQL <c>SELECT DISTINCT</c>。
        /// </remarks>
        public override async Task<List<object?>> DistinctFieldValuesAsync(
            DynamicFilter? filter,
            string fieldPath,
            int limit = 0,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fieldPath)) return [];

            var predicate = filter.ToPredicate<T>();
            // 只取该字段所在的 jsonb 列，避免把整行（含大 jsonb）拉回来。
            var rootSegment = DynamicPathAccessor.SplitPath(fieldPath).FirstOrDefault();
            if (rootSegment is null) return [];

            var rootProperty = DynamicPathAccessor.FindProperty(typeof(T), rootSegment);
            if (rootProperty is null) return [];

            if (!DynamicPathAccessor.IsDynamicContainer(rootProperty.PropertyType))
            {
                // 非 jsonb 路径：可用 EF 的 Distinct 走数据库去重。
                var query = Queryable.Where(predicate);
                // 通过反射拿到属性访问表达式后投影，保持强类型。
                var selector = BuildProjection(rootProperty);
                var values = await query.Select(selector).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
                var filtered = values.Where(x => x is not null).ToList();
                return limit > 0 ? filtered.Take(limit).Cast<object?>().ToList() : filtered.Cast<object?>().ToList();
            }

            var rows = await Queryable.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
            var path = DynamicPathAccessor.SplitPath(fieldPath);

            var result = new List<object?>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var value = DynamicPathValueReader.Read(row, path);
                foreach (var flattened in DynamicPathValueReader.Flatten(value))
                {
                    if (!seen.Add(DynamicPathValueReader.ToDedupKey(flattened))) continue;
                    result.Add(flattened);
                    if (limit > 0 && result.Count >= limit) return result;
                }
            }

            return result;
        }

        /// <summary>
        /// 构造从 <typeparamref name="T"/> 到指定属性值的投影表达式。
        /// </summary>
        private static Expression<Func<T, object?>> BuildProjection(System.Reflection.PropertyInfo property)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var access = Expression.Property(parameter, property);
            var boxed = property.PropertyType.IsValueType
                ? Expression.Convert(access, typeof(object))
                : (Expression)access;
            return Expression.Lambda<Func<T, object?>>(boxed, parameter);
        }

        #endregion

        #region 写入

        /// <summary>
        /// </summary>
        /// <remarks>
        /// 因此当时的 <c>Insert</c> 是 <c>InsertCore(EnsureId(entity))</c>；
        /// 换到 PostgreSQL 后主键是普通 <c>text</c> 列，数据库不会代生成，
        /// 少了这一步就会插入 <c>Id = ''</c>，第二条直接撞主键唯一约束
        /// （EF 侧则先报 <c>another instance with the same key value for {'Id'}</c>）。
        /// 所以四个 Insert 重载都必须在这里补主键。
        /// </remarks>
        public override void Insert(T entity)
        {
            EnsureId(entity);
            AttachForWrite(entity);
            CommitOwnScope();
        }

        /// <inheritdoc />
        public override void Insert(IEnumerable<T> entities)
        {
            Context.Set<T>().AddRange(EnsureId(entities));
            CommitOwnScope();
        }

        /// <inheritdoc />
        public override async Task InsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            EnsureId(entity);
            Context.Set<T>().Add(entity);
            await CommitOwnScopeAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            await Context.Set<T>().AddRangeAsync(EnsureId(entities), cancellationToken).ConfigureAwait(false);
            await CommitOwnScopeAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// </summary>
        /// <remarks>
        /// EF Core 的 <c>Update</c> 在「另一个同键实例已在跟踪中」时会抛
        /// <c>another instance with the same key value for {'Id'} is already being tracked</c>。
        /// </remarks>
        public override void Replace(T entity)
        {
            DetachTracked(entity.Id);
            Context.Set<T>().Update(entity);
            CommitOwnScope();
        }

        /// <inheritdoc />
        public override async Task ReplaceAsync(T entity, CancellationToken cancellationToken = default)
        {
            DetachTracked(entity.Id);
            Context.Set<T>().Update(entity);
            await CommitOwnScopeAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 摘掉上下文中同主键的跟踪实例，避免 Update 时触发主键冲突。
        /// </summary>
        private void DetachTracked(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            foreach (var entry in Context.ChangeTracker.Entries<T>()
                         .Where(x => x.Entity.Id == id)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        /// <inheritdoc />
        public override Task<int> UpdateAsync(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default)
            => InWriteScopeAsync(() => BatchUpdateAsync(predicate, setters, cancellationToken));

        /// <inheritdoc />
        public override Task<int> UpdateAsync(
            string id,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default)
            => UpdateAsync(x => x.Id == id, setters, cancellationToken);

        /// <inheritdoc />
        public override Task<int> UpdateManyAsync(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default)
            => UpdateAsync(predicate, setters, cancellationToken);

        /// <inheritdoc />
        public override int UpdateMany(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters)
            => InWriteScope(() => Context.Set<T>().Where(predicate).ExecuteUpdate(setters));

        /// <inheritdoc />
        public override int UpdateMany(
            DynamicFilter filter,
            Action<UpdateSettersBuilder<T>> setters)
            => UpdateMany(filter.ToPredicate<T>(), setters);

        /// <inheritdoc />
        public override int Update(
            string id,
            Action<UpdateSettersBuilder<T>> setters)
            => UpdateMany(x => x.Id == id, setters);

        /// <inheritdoc />
        public override Task<int> UpdateManyAsync(
            DynamicFilter filter,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken = default)
            => UpdateAsync(filter.ToPredicate<T>(), setters, cancellationToken);

        /// <inheritdoc />
        public override void Delete(T entity)
        {
            Context.Set<T>().Remove(entity);
            CommitOwnScope();
        }

        /// <inheritdoc />
        public override async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            Context.Set<T>().Remove(entity);
            await CommitOwnScopeAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override Task<int> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => InWriteScopeAsync(() => Context.Set<T>().Where(predicate).ExecuteDeleteAsync(cancellationToken));

        /// <inheritdoc />
        public override Task<int> DeleteManyAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => DeleteManyAsync(filter.ToPredicate<T>(), cancellationToken);

        /// <inheritdoc />
        public override int Delete(string id)
            => InWriteScope(() => Context.Set<T>().Where(x => x.Id == id).ExecuteDelete());

        /// <inheritdoc />
        public override int Delete(IEnumerable<string> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return 0;
            return InWriteScope(() => Context.Set<T>().Where(x => idList.Contains(x.Id)).ExecuteDelete());
        }

        /// <inheritdoc />
        public override int Delete(DynamicFilter filter)
            => InWriteScope(() => Context.Set<T>().Where(filter.ToPredicate<T>()).ExecuteDelete());

        /// <inheritdoc />
        public override Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default)
            => InWriteScopeAsync(() => Context.Set<T>().Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken));

        /// <inheritdoc />
        public override Task<int> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return Task.FromResult(0);
            return InWriteScopeAsync(() => Context.Set<T>().Where(x => idList.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken));
        }

        /// <inheritdoc />
        public override Task<int> DeleteAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => DeleteManyAsync(filter.ToPredicate<T>(), cancellationToken);

        /// <inheritdoc />
        public override Task<int> SoftDeleteManyAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return Task.FromResult(0);
            return SoftDeleteManyAsync(x => idList.Contains(x.Id), cancellationToken);
        }

        /// <inheritdoc />
        public override Task<int> SoftDeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            EnsureSoftDeletable();
            return InWriteScopeAsync(() => BatchUpdateAsync(
                predicate,
                setters => setters.SetProperty(entity => ((IDeleteFlag)entity).DeleteFlag, true),
                cancellationToken));
        }

        /// <inheritdoc />
        public override Task<int> SoftDeleteManyAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
            => SoftDeleteManyAsync(filter.ToPredicate<T>(), cancellationToken);

        #endregion

        #region Helper

        /// <summary>
        /// 应用查询选项：过滤 → 排序 → 分页。
        /// </summary>
        private static IQueryable<T> Apply(QueryFindOptions<T> options, IQueryable<T> source)
        {
            var query = options.Filter is null ? source : source.Where(options.Filter);
            query = query.OrderBy(options.Sort);

            if (options.Skip > 0) query = query.Skip(options.Skip);
            var take = options.GetEffectiveTake();
            // Take 为 int.MaxValue 时视为不限量，避免生成巨大的 LIMIT 常量。
            if (take > 0 && take < int.MaxValue) query = query.Take(take);

            return query;
        }

        /// <summary>
        /// 执行批量更新。ExecuteUpdate 会直接走数据库，不经过变更跟踪。
        /// </summary>
        private Task<int> BatchUpdateAsync(
            Expression<Func<T, bool>> predicate,
            Action<UpdateSettersBuilder<T>> setters,
            CancellationToken cancellationToken)
            => Context.Set<T>().Where(predicate).ExecuteUpdateAsync(setters, cancellationToken);

        /// <summary>
        /// 在写作用域中执行操作（同步版）：已在环境事务中则直接执行，否则开启短事务并在成功后提交。
        /// </summary>
        private TResult InWriteScope<TResult>(Func<TResult> operation)
        {
            if (TransactionScope.IsInTransactionFor(Context))
            {
                return operation();
            }

            using var scope = new TransactionScope(Context);
            var result = operation();
            scope.CommitTransaction();
            return result;
        }

        /// <summary>
        /// 在写作用域中执行操作：已在环境事务中则直接执行，否则开启短事务并在成功后提交。
        /// </summary>
        private async Task<TResult> InWriteScopeAsync<TResult>(Func<Task<TResult>> operation)
        {
            if (TransactionScope.IsInTransactionFor(Context))
            {
                return await operation().ConfigureAwait(false);
            }

            await using var scope = new TransactionScope(Context);
            var result = await operation().ConfigureAwait(false);
            await scope.CommitTransactionAsync().ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// 提交变更：已处于环境事务中时只 SaveChanges（由外层提交），否则自建事务提交。
        /// </summary>
        private void CommitOwnScope()
        {
            if (TransactionScope.IsInTransactionFor(Context))
            {
                Context.SaveChanges();
                return;
            }

            using var scope = new TransactionScope(Context);
            scope.CommitTransaction();
        }

        /// <summary>
        /// 异步提交变更：语义同 <see cref="CommitOwnScope"/>。
        /// </summary>
        private async Task CommitOwnScopeAsync(CancellationToken cancellationToken)
        {
            if (TransactionScope.IsInTransactionFor(Context))
            {
                await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await using var scope = new TransactionScope(Context);
            await scope.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 校验实体是否支持软删除。
        /// </summary>
        private static void EnsureSoftDeletable()
        {
            if (!typeof(IDeleteFlag).IsAssignableFrom(typeof(T)))
            {
                throw new NotSupportedException($"{typeof(T).Name} 未实现 IDeleteFlag，无法软删除。");
            }
        }

        /// <summary>
        /// 新增时若实体处于 Detached 状态需要显式 Add；否则 Update 会因为找不到已有实体而抛错。
        /// </summary>
        private void AttachForWrite(T entity)
        {
            var entry = Context.Entry(entity);
            if (entry.State == EntityState.Detached) Context.Set<T>().Add(entity);
        }

        #endregion
    }
}
