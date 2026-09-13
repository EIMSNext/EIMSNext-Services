using EIMSNext.Core.Mongo;

using MongoDB.Driver;

namespace EIMSNext.Core.Mongo
{
    /// <summary>
    /// Mongo 事务作用域，管理事务的开启、提交、回滚与会话生命周期。
    /// </summary>
    public class MongoTransactionScope : IDisposable, IAsyncDisposable
    {
        private const string TransientTransactionError = "TransientTransactionError";
        private const string UnknownTransactionCommitResult = "UnknownTransactionCommitResult";
        private const int WriteConflictCode = 112;
        private sealed class ScopeState
        {
            public IClientSessionHandle? SessionHandle { get; init; }
            public bool Enabled { get; init; }
            public List<Func<Task>> AfterCommit { get; } = [];
        }

        private static readonly AsyncLocal<ScopeState?> _currentState = new();
        private readonly bool _isRootScope;
        private bool _completed = false;
        private List<Func<Task>>? _committedCallbacks;

        /// <summary>
        /// 初始化 <see cref="MongoTransactionScope"/> 类的新实例。
        /// </summary>
        /// <param name="dbContex">数据库上下文。</param>
        /// <param name="transOptions">事务选项，可为空。</param>
        /// <param name="enabled">是否由 root scope 启用事务。</param>
        public MongoTransactionScope(IMongoDbContex dbContex, TransactionOptions? transOptions = null, bool enabled = true)
        {
            if (_currentState.Value is { } parent)
            {
                SessionHandle = parent.SessionHandle;
                _isRootScope = false;
                return;
            }

            _isRootScope = true;
            if (!enabled)
            {
                SessionHandle = null;
                _currentState.Value = new ScopeState { Enabled = false };
                return;
            }

            var config = dbContex.TransactionConfiguration ?? new MongoDbConfiguration();
            var options = transOptions ?? new TransactionOptions(
                readConcern: ParseReadConcern(config.TransactionReadConcern),
                writeConcern: ParseWriteConcern(config.TransactionWriteConcern));
            var session = dbContex.StartSession();
            session.StartTransaction(options);
            SessionHandle = session;
            _currentState.Value = new ScopeState { Enabled = true, SessionHandle = session };
        }

        /// <summary>
        /// 获取当前事务的会话句柄。
        /// </summary>
        public static IClientSessionHandle? Transaction => _currentState.Value?.SessionHandle;

        /// <summary>
        /// 获取一个值，指示当前是否处于事务中。
        /// </summary>
        public static bool IsInTransaction => Transaction != null && Transaction.IsInTransaction;

        /// <summary>
        /// 临时抑制当前异步上下文中的事务。退出作用域后恢复原状态。
        /// 用于明确允许独立提交的弱一致性操作。
        /// </summary>
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
        /// 获取事务会话句柄。
        /// </summary>
        public IClientSessionHandle? SessionHandle { get; private set; }
        //public bool IsInTransaction => SessionHandle.IsInTransaction;

        /// <summary>
        /// 提交事务。
        /// </summary>
        public void CommitTransaction()
        {
            if (_isRootScope && SessionHandle?.IsInTransaction == true)
            {
                SessionHandle.CommitTransaction();
                _completed = true;
                _committedCallbacks = _currentState.Value?.AfterCommit.ToList();
                _currentState.Value?.AfterCommit.Clear();
            }
        }

        /// <summary>异步提交事务，并在提交后安排回调。</summary>
        public async Task CommitTransactionAsync()
        {
            if (_isRootScope && SessionHandle?.IsInTransaction == true)
            {
                await SessionHandle.CommitTransactionAsync().ConfigureAwait(false);
                _completed = true;
                _committedCallbacks = _currentState.Value?.AfterCommit.ToList();
                _currentState.Value?.AfterCommit.Clear();
            }
        }

        /// <summary>
        /// 注册事务提交后的回调。
        /// </summary>
        /// <param name="callback">提交后的回调。</param>
        public static void RegisterAfterCommit(Func<Task> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (_currentState.Value is { Enabled: true } state)
            {
                state.AfterCommit.Add(callback);
                return;
            }

            callback().GetAwaiter().GetResult();
        }

        /// <summary>异步注册事务提交后的回调；无事务时立即异步执行。</summary>
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

        private static ReadConcern ParseReadConcern(string? value) => (value ?? "majority").ToLowerInvariant() switch
        {
            "local" => ReadConcern.Local, "majority" => ReadConcern.Majority,
            "snapshot" => ReadConcern.Snapshot,
            _ => throw new InvalidOperationException($"不支持的 Mongo TransactionReadConcern: {value}")
        };

        private static WriteConcern ParseWriteConcern(string? value) => (value ?? "majority").ToLowerInvariant() switch
        {
            "acknowledged" => WriteConcern.Acknowledged,
            "majority" => WriteConcern.WMajority, "journaled" => new WriteConcern(journal: true),
            _ => throw new InvalidOperationException($"不支持的 Mongo TransactionWriteConcern: {value}")
        };

        /// <summary>执行事务并对瞬态冲突进行重试。</summary>
        public static Task ExecuteWithRetryAsync(
            IMongoDbContex dbContext,
            Func<IClientSessionHandle, Task> operation,
            TransactionOptions? transactionOptions = null,
            int? maxRetries = null,
            CancellationToken cancellationToken = default)
            => ExecuteWithRetryAsync<object?>(dbContext, async session => { await operation(session).ConfigureAwait(false); return null; }, transactionOptions, maxRetries, cancellationToken);

        public static TResult ExecuteWithRetry<TResult>(IMongoDbContex dbContext, Func<IClientSessionHandle, TResult> operation, TransactionOptions? transactionOptions = null, int? maxRetries = null)
        {
            var retryCount = maxRetries ?? dbContext.TransactionConfiguration.TransactionMaxRetries;
            if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
            Exception? last = null;
            for (var attempt = 0; attempt <= retryCount; attempt++)
            {
                using var scope = new MongoTransactionScope(dbContext, transactionOptions);
                try
                {
                    var result = operation(scope.SessionHandle!);
                    CommitWithRetry(scope);
                    return result;
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < retryCount)
                {
                    last = ex;
                    Thread.Sleep(TimeSpan.FromMilliseconds(Math.Min(1000, 50 * Math.Pow(2, attempt)) + Random.Shared.Next(25)));
                }
            }
            throw last!;
        }

        private static void CommitWithRetry(MongoTransactionScope scope)
        {
            for (var attempt = 0; ; attempt++)
            {
                try { scope.CommitTransaction(); return; }
                catch (Exception ex) when (HasLabel(ex, UnknownTransactionCommitResult) && attempt < 1)
                {
                    Thread.Sleep(25);
                }
            }
        }

        /// <summary>执行有返回值的事务并对瞬态冲突进行重试。</summary>
        public static async Task<TResult> ExecuteWithRetryAsync<TResult>(
            IMongoDbContex dbContext,
            Func<IClientSessionHandle, Task<TResult>> operation,
            TransactionOptions? transactionOptions = null,
            int? maxRetries = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(operation);
            var retryCount = maxRetries ?? dbContext.TransactionConfiguration.TransactionMaxRetries;
            if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
            if (IsInTransaction)
                return await operation(Transaction!).ConfigureAwait(false);

            Exception? last = null;
            for (var attempt = 0; attempt <= retryCount; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var scope = new MongoTransactionScope(dbContext, transactionOptions);
                try
                {
                    var result = await operation(scope.SessionHandle!).ConfigureAwait(false);
                    await CommitWithRetryAsync(scope, cancellationToken).ConfigureAwait(false);
                    return result;
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < retryCount)
                {
                    last = ex;
                    await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                }
            }
            throw last!;
        }

        private static async Task CommitWithRetryAsync(MongoTransactionScope scope, CancellationToken cancellationToken)
        {
            const int maxCommitRetries = 1;
            for (var attempt = 0; ; attempt++)
            {
                try { await scope.CommitTransactionAsync().ConfigureAwait(false); return; }
                catch (Exception ex) when (HasLabel(ex, UnknownTransactionCommitResult))
                {
                    if (attempt >= maxCommitRetries) throw;
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static bool IsTransient(Exception ex)
            => HasLabel(ex, TransientTransactionError) || ex is MongoWriteException w && w.WriteError?.Code == WriteConflictCode || ex is MongoBulkWriteException b && b.WriteErrors.Any(e => e.Code == WriteConflictCode);

        private static bool HasLabel(Exception ex, string label)
            => ex is MongoException m && m.HasErrorLabel(label) || ex.InnerException != null && HasLabel(ex.InnerException, label);

        private static Task DelayAsync(int attempt, CancellationToken cancellationToken)
            => Task.Delay(TimeSpan.FromMilliseconds(Math.Min(1000, 50 * Math.Pow(2, attempt)) + Random.Shared.Next(25)), cancellationToken);

        /// <summary>
        /// 中止事务。
        /// </summary>
        public void AbortTransaction()
        {
            if (_isRootScope && SessionHandle?.IsInTransaction == true)
                SessionHandle.AbortTransaction();
        }

        /// <summary>
        /// 释放事务作用域，必要时中止未提交的事务。
        /// </summary>
        public void Dispose()
        {
            if (_isRootScope)
            {
                var callbacks = _completed ? _committedCallbacks?.ToArray() : [];
                try
                {
                    if (!_completed)
                        AbortTransaction();
                }
                finally
                {
                    _currentState.Value = null;
                    SessionHandle?.Dispose();
                }

                foreach (var callback in callbacks ?? [])
                {
                    try
                    {
                        callback().GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // After-commit work must not turn a committed business transaction into a failure.
                    }
                }
            }
        }

        /// <summary>异步释放事务作用域，并执行已提交回调。</summary>
        public async ValueTask DisposeAsync()
        {
            if (!_isRootScope)
                return;

            var callbacks = _completed ? _committedCallbacks?.ToArray() : [];
            try
            {
                if (!_completed && SessionHandle?.IsInTransaction == true)
                    await SessionHandle.AbortTransactionAsync().ConfigureAwait(false);
            }
            finally
            {
                _currentState.Value = null;
                SessionHandle?.Dispose();
            }

            foreach (var callback in callbacks ?? [])
            {
                try
                {
                    await callback().ConfigureAwait(false);
                }
                catch
                {
                    // After-commit work must not turn a committed transaction into a failure.
                }
            }
        }
    }
}
