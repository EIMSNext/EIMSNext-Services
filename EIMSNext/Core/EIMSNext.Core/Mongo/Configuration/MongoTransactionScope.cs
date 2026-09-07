using EIMSNext.Core.Mongo;

using MongoDB.Driver;

namespace EIMSNext.Core.Mongo
{
    /// <summary>
    /// Mongo 事务作用域，管理事务的开启、提交、回滚与会话生命周期。
    /// </summary>
    public class MongoTransactionScope : IDisposable
    {
        private static readonly AsyncLocal<IClientSessionHandle?> _currentSession = new AsyncLocal<IClientSessionHandle?>();
        private bool _isRootScope;
        private bool _completed = false;
        private readonly List<Func<Task>> _afterCommit = [];
        private List<Func<Task>>? _committedCallbacks;
        private static readonly AsyncLocal<List<Func<Task>>?> _afterCommitCallbacks = new();

        /// <summary>
        /// 初始化 <see cref="MongoTransactionScope"/> 类的新实例。
        /// </summary>
        /// <param name="dbContex">数据库上下文。</param>
        /// <param name="transOptions">事务选项，可为空。</param>
        public MongoTransactionScope(IMongoDbContex dbContex, TransactionOptions? transOptions = null)
        {
            if (_currentSession.Value == null)
            {
                var options = transOptions ?? new TransactionOptions(readConcern: ReadConcern.Majority, writeConcern: WriteConcern.WMajority);
                SessionHandle = dbContex.StartSession();
                SessionHandle.StartTransaction(options);

                _currentSession.Value = SessionHandle;
                _afterCommitCallbacks.Value = _afterCommit;
                _isRootScope = true;
            }
            else
            {
                SessionHandle = _currentSession.Value;
                _isRootScope = false;
            }
        }

        /// <summary>
        /// 获取当前事务的会话句柄。
        /// </summary>
        public static IClientSessionHandle? Transaction => _currentSession.Value;

        /// <summary>
        /// 获取一个值，指示当前是否处于事务中。
        /// </summary>
        public static bool IsInTransaction => Transaction != null && Transaction.IsInTransaction;

        /// <summary>
        /// 获取事务会话句柄。
        /// </summary>
        public IClientSessionHandle SessionHandle { get; private set; }
        //public bool IsInTransaction => SessionHandle.IsInTransaction;

        /// <summary>
        /// 提交事务。
        /// </summary>
        public void CommitTransaction()
        {
            if (_isRootScope && SessionHandle.IsInTransaction)
            {
                SessionHandle.CommitTransaction();
                _completed = true;
                _committedCallbacks = _afterCommit.ToList();
                _afterCommit.Clear();
                _afterCommitCallbacks.Value = null;
            }
        }

        /// <summary>
        /// 注册事务提交后的回调。
        /// </summary>
        /// <param name="callback">提交后的回调。</param>
        public static void RegisterAfterCommit(Func<Task> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (_afterCommitCallbacks.Value is { } callbacks)
            {
                callbacks.Add(callback);
                return;
            }

            callback().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 中止事务。
        /// </summary>
        public void AbortTransaction()
        {
            if (_isRootScope && SessionHandle.IsInTransaction)
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
                    _currentSession.Value = null;
                    _afterCommitCallbacks.Value = null;
                    SessionHandle.Dispose();
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
