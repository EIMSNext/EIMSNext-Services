using EIMSNext.Core.Mongo;

using MongoDB.Driver;

namespace EIMSNext.Core.Mongo
{
    /// <summary>
    /// Mongo 事务作用域，管理事务的开启、提交、回滚与会话生命周期。
    /// </summary>
    public class MongoTransactionScope : IDisposable
    {
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

            var options = transOptions ?? new TransactionOptions(readConcern: ReadConcern.Majority, writeConcern: WriteConcern.WMajority);
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
    }
}
