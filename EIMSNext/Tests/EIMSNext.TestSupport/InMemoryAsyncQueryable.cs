using System.Collections;
using System.Linq.Expressions;

namespace EIMSNext.TestSupport
{
    /// <summary>
    /// 内存版 <see cref="IQueryable{T}"/>，同时实现 <see cref="IAsyncEnumerable{T}"/>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用途：生产代码里出现了
    /// <c>Repository.Queryable.IgnoreQueryFilters().Where(...).ToListAsync()</c> 这类<b>异步 LINQ</b> 写法。
    /// EF Core 的异步算子要求源必须实现 <see cref="IAsyncEnumerable{T}"/>，而
    /// <c>List&lt;T&gt;.AsQueryable()</c>（<c>EnumerableQuery</c>）并不实现，
    /// 于是内存假仓储会抛
    /// <c>The source 'IQueryable' doesn't implement 'IAsyncEnumerable&lt;T&gt;'</c>。
    /// </para>
    /// <para>
    /// 本类型把普通序列包成「既同步可枚举、又异步可枚举」的查询源，
    /// 让内存桩也能承接异步算子。
    /// </para>
    /// <para>
    /// 实现要点：包装器只保存表达式的<b>文本</b>与自己的 Provider，而根表达式仍然是
    /// 标准 <see cref="EnumerableQuery"/> 的常量，因此 <see cref="Queryable"/> 算子
    /// （<c>Where</c>/<c>Select</c>/<c>OrderBy</c>…）会在标准 Provider 上构建表达式，
    /// 由本 Provider 编译执行，不会出现「创建查询 → 再编译 → 再创建查询」的递归。
    /// </para>
    /// </remarks>
    public sealed class InMemoryAsyncQueryable<T> : IQueryable<T>, IAsyncEnumerable<T>, IOrderedQueryable<T>
    {
        private readonly IQueryProvider _provider;

        /// <summary>
        /// 初始化 <see cref="InMemoryAsyncQueryable{T}"/> 类的新实例。
        /// </summary>
        public InMemoryAsyncQueryable(IEnumerable<T> source)
            : this(source.AsQueryable().Expression, new InMemoryAsyncQueryProvider())
        {
        }

        internal InMemoryAsyncQueryable(Expression expression, IQueryProvider provider)
        {
            Expression = expression;
            _provider = provider;
        }

        /// <inheritdoc />
        public Type ElementType => typeof(T);

        /// <inheritdoc />
        public Expression Expression { get; }

        /// <inheritdoc />
        public IQueryProvider Provider => _provider;

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
            => _provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new AsyncEnumerator(GetEnumerator());

        private sealed class AsyncEnumerator(IEnumerator<T> inner) : IAsyncEnumerator<T>
        {
            public T Current => inner.Current;

            public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());

            public ValueTask DisposeAsync()
            {
                inner.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>
    /// <see cref="InMemoryAsyncQueryable{T}"/> 的查询提供程序：构建查询时返回包装器
    /// （保住异步可枚举能力），执行时把表达式编译成标准 LINQ 调用。
    /// </summary>
    public sealed class InMemoryAsyncQueryProvider : IQueryProvider
    {
        /// <inheritdoc />
        public IQueryable CreateQuery(Expression expression)
            => CreateQuery<object>(expression);

        /// <inheritdoc />
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new InMemoryAsyncQueryable<TElement>(expression, this);

        /// <inheritdoc />
        public object? Execute(Expression expression)
            => Expression.Lambda(expression).Compile().DynamicInvoke();

        /// <inheritdoc />
        public TResult Execute<TResult>(Expression expression)
            => (TResult)Expression.Lambda(expression).Compile().DynamicInvoke()!;
    }
}
