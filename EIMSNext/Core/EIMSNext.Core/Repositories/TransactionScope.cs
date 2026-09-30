using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EIMSNext.Core.Repositories
{
    /// <summary>
    /// 保留同名 API（<see cref="IsInTransaction"/>、<see cref="RegisterAfterCommitAsync(System.Func{System.Threading.Tasks.Task})"/>、
    /// <see cref="ExecuteWithRetryAsync{TResult}"/>），去掉 <c>SessionHandle</c>。
    /// </summary>
    /// <remarks>
    /// </remarks>
    public sealed class TransactionScope : IDisposable, IAsyncDisposable
    {
        private sealed class ScopeState
        {
            public DbContext? Context { get; init; }
            public ScopeState? Parent { get; init; }
            public IDbContextTransaction? Transaction { get; set; }
            public bool Enabled { get; set; }
            public List<Func<Task>> AfterCommit { get; } = [];
        }

        private static readonly AsyncLocal<ScopeState?> _currentState = new();

        private readonly DbContext _dbContext;
        private readonly ScopeState? _parentState;
        private readonly bool _isRootScope;
        private readonly IDbContextTransaction? _ownTransaction;
        private bool _completed;
        private List<Func<Task>>? _committedCallbacks;

        /// <summary>
        /// 初始化 <see cref="TransactionScope"/> 类的新实例。
        /// </summary>
        /// <param name="dbContext">工作单元所属的数据库上下文，为 null 时只登记标记（测试替身）。</param>
        /// <param name="enabled">为 <c>false</c> 时明确表达「这段操作不需要事务」：不开真实事务，但提交时仍会落库。</param>
        public TransactionScope(DbContext dbContext, bool enabled = true)
        {
            _dbContext = dbContext;

            if (_currentState.Value is { } parent && ReferenceEquals(parent.Context, dbContext))
            {
                _isRootScope = false;
                return;
            }

            _isRootScope = true;
            _parentState = _currentState.Value;
            if (!enabled)
            {
                _currentState.Value = new ScopeState { Context = dbContext, Parent = _parentState, Enabled = false };
                return;
            }

            _ownTransaction = dbContext.Database.BeginTransaction();
            _currentState.Value = new ScopeState
            {
                Context = dbContext,
                Parent = _parentState,
                Enabled = true,
                Transaction = _ownTransaction
            };
        }

        /// <summary>
        /// 事务创建工厂的注入点。持久化层可换成带重试与提交后回调策略的实现；
        /// 默认是纯 EF Core 事务。
        /// </summary>
        public static Func<DbContext, TransactionScope> Factory { get; set; }
            = context => new TransactionScope(context);

        /// <summary>
        /// 获取当前事务；无事务时为 null。
        /// </summary>
        public static IDbContextTransaction? Transaction => _currentState.Value?.Transaction;

        /// <summary>
        /// 获取一个值，指示当前是否处于事务中。
        /// </summary>
        public static bool IsInTransaction => _currentState.Value?.Transaction is not null;

        /// <summary>
        /// 判断指定上下文是否持有当前 ambient 事务。
        /// </summary>
        public static bool IsInTransactionFor(DbContext dbContext)
            => _currentState.Value is { Transaction: not null, Context: { } context }
                && ReferenceEquals(context, dbContext);

        /// <summary>
        /// 临时抑制当前异步上下文中的事务，用于明确允许独立提交的弱一致性操作。
        /// </summary>
        /// <returns>释放后恢复原事务上下文的句柄。</returns>
        public static IDisposable SuppressAmbient()
        {
            var previous = _currentState.Value;
            _currentState.Value = null;
            return new AmbientSuppression(previous);
        }

        private sealed class AmbientSuppression(ScopeState? previous) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _currentState.Value = previous;
            }
        }

        /// <summary>
        /// 同步提交事务。只有最外层作用域真正提交。
        /// </summary>
        public void CommitTransaction()
        {
            if (!_isRootScope) return;
            if (_ownTransaction is null)
            {
                CommitWithoutTransaction();
                return;
            }

            _dbContext.SaveChanges();
            _ownTransaction.Commit();
            _completed = true;
            CaptureAfterCommit();
        }

        /// <summary>
        /// 异步提交事务，并暂存提交后回调。
        /// </summary>
        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRootScope) return;
            if (_ownTransaction is null)
            {
                await CommitWithoutTransactionAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _ownTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _completed = true;
            CaptureAfterCommit();
        }

        /// <summary>
        /// 无事务模式（<c>TransNeeded = false</c>）下的提交：不起事务，但工作单元仍要落库。
        /// </summary>
        /// <remarks>
        /// 落库只发生在提交阶段（<c>AddCoreAsync</c> 之类只是把实体放进变更跟踪器）。
        /// 若此处因为「没有自己的事务」直接返回，调用方会拿到成功响应而库里一条记录都没有。
        /// 没有 DbContext（仓储替身）时无工作单元可刷，视为已提交。
        /// </remarks>
        private void CommitWithoutTransaction()
        {
            _dbContext?.SaveChanges();
            _completed = true;
            CaptureAfterCommit();
        }

        private async Task CommitWithoutTransactionAsync(CancellationToken cancellationToken)
        {
            if (_dbContext is not null)
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            _completed = true;
            CaptureAfterCommit();
        }

        /// <summary>
        /// 注册事务提交后的回调。无事务时立即执行，
        /// 这样调用方不必区分「是否在事务里」两种路径。
        /// </summary>
        /// <returns>表示回调登记或执行完成的任务。</returns>
        public static Task RegisterAfterCommitAsync(Func<Task> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (_currentState.Value is { Enabled: true } state)
            {
                state.AfterCommit.Add(callback);
                return Task.CompletedTask;
            }

            return callback();
        }

        /// <summary>
        /// 注册绑定到指定 DbContext 的提交后回调。
        /// </summary>
        public static Task RegisterAfterCommitAsync(DbContext dbContext, Func<Task> callback)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(callback);
            if (IsInTransactionFor(dbContext) && _currentState.Value is { Enabled: true } state)
            {
                state.AfterCommit.Add(callback);
                return Task.CompletedTask;
            }

            return callback();
        }

        /// <summary>
        /// 同步版本，语义同 <see cref="RegisterAfterCommitAsync(System.Func{System.Threading.Tasks.Task})"/>。
        /// </summary>
        public static void RegisterAfterCommit(Func<Task> callback)
            => RegisterAfterCommitAsync(callback).GetAwaiter().GetResult();

        /// <summary>同步版本，绑定到指定 DbContext。</summary>
        public static void RegisterAfterCommit(DbContext dbContext, Func<Task> callback)
            => RegisterAfterCommitAsync(dbContext, callback).GetAwaiter().GetResult();

        /// <summary>
        /// 在事务中执行操作，并对瞬态冲突（序列化失败 40001、死锁 40P01）自动重试。
        /// 已处于事务中时直接执行，不重复开启。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRetries"/> 为负数。</exception>
        public static async Task<TResult> ExecuteWithRetryAsync<TResult>(
            DbContext dbContext,
            Func<Task<TResult>> operation,
            int maxRetries = 3,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(operation);

            // 必须显式拒绝。若放任其进入下面的 for 循环，循环体一次都不会执行，
            // 最终 `throw last!` 会抛出毫无信息量的 NullReferenceException。
            if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));

            if (IsInTransactionFor(dbContext)) return await operation().ConfigureAwait(false);

            Exception? last = null;
            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var scope = new TransactionScope(dbContext);
                try
                {
                    var result = await operation().ConfigureAwait(false);
                    await scope.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
                    return result;
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < maxRetries)
                {
                    // 重试前必须让上下文脱离失败事务的跟踪状态，
                    // 否则下一次操作会带着旧实体继续抛同样的异常。
                    dbContext.ChangeTracker.Clear();
                    last = ex;
                    await Task.Delay(
                            TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt) + Random.Shared.Next(25)),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            throw last!;
        }

        /// <summary>
        /// 无返回值版本。
        /// </summary>
        public static Task ExecuteWithRetryAsync(
            DbContext dbContext,
            Func<Task> operation,
            int maxRetries = 3,
            CancellationToken cancellationToken = default)
            => ExecuteWithRetryAsync<object?>(
                dbContext,
                async () => { await operation().ConfigureAwait(false); return null; },
                maxRetries,
                cancellationToken);

        /// <inheritdoc />
        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        /// <summary>
        /// 释放作用域；未提交则回滚，并执行已登记的提交后回调。
        /// </summary>
        /// <remarks>
        /// 本方法刻意<b>不写成 <c>async</c></b>：环境状态必须在「调用方所在的执行上下文」里
        /// 同步清除并立即返回，真正的释放逻辑交给 <see cref="DisposeCoreAsync"/>。
        /// <para>
        /// 原因：对 <see cref="AsyncLocal{T}"/> 的写入如果发生在 <c>async</c> 方法体内，
        /// 不会传播回调用方（异步状态机与调用方各自持有执行上下文）。实测把
        /// <c>_currentState.Value = null</c> 放在 async 方法体的第一行（任何 <c>await</c> 之前）
        /// <b>依然无效</b>，于是 <c>using</c> 块结束后 <see cref="Transaction"/> 仍会返回
        /// 一个已释放的事务。
        /// </para>
        /// <para>
        /// 反过来，提交路径（<see cref="CommitTransactionAsync"/> /
        /// <see cref="RegisterAfterCommitAsync(System.Func{System.Threading.Tasks.Task})"/>）修改的是 <c>ScopeState</c> 这个共享对象的
        /// 成员，靠引用传递天然可见，不受该限制。
        /// </para>
        /// </remarks>
        public ValueTask DisposeAsync()
        {
            if (!_isRootScope) return ValueTask.CompletedTask;

            _currentState.Value = _parentState;

            return DisposeCoreAsync();
        }

        private async ValueTask DisposeCoreAsync()
        {
            var callbacks = _completed ? _committedCallbacks?.ToArray() : [];
            try
            {
                if (!_completed && _ownTransaction is not null)
                {
                    await _ownTransaction.RollbackAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                if (_ownTransaction is not null) await _ownTransaction.DisposeAsync().ConfigureAwait(false);
            }

            foreach (var callback in callbacks ?? [])
            {
                try
                {
                    await callback().ConfigureAwait(false);
                }
                catch
                {
                    // 提交后动作失败不能把已提交的业务事务变成失败。
                }
            }
        }

        /// <summary>
        /// 把当前作用域已登记的提交后回调摘出来，交给释放阶段执行。
        /// </summary>
        private void CaptureAfterCommit()
        {
            _committedCallbacks = _currentState.Value?.AfterCommit.ToList();
            _currentState.Value?.AfterCommit.Clear();
        }

        /// <summary>
        /// 判定是否为可重试的瞬态冲突。
        /// </summary>
        /// <param name="ex">异常。</param>
        /// <returns>可重试时为 true。</returns>
        private static bool IsTransient(Exception ex)
        {
            for (var current = ex; current is not null; current = current.InnerException)
            {
                if (current is System.Data.Common.DbException db
                    && db.GetType().GetProperty("SqlState")?.GetValue(db) is string sqlState
                    && sqlState is "40001" or "40P01") return true;
                if (current.GetType().Name.Contains("DbUpdateConcurrencyException", StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
