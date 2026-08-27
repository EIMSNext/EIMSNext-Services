using EIMSNext.Core.Mongo;

using MongoDB.Driver;

namespace EIMSNext.Core.Mongo
{
    public class MongoTransactionScope : IDisposable
    {
        private static readonly AsyncLocal<IClientSessionHandle?> _currentSession = new AsyncLocal<IClientSessionHandle?>();
        private bool _isRootScope;
        private bool _completed = false;
        private readonly List<Func<Task>> _afterCommit = [];
        private List<Func<Task>>? _committedCallbacks;
        private static readonly AsyncLocal<List<Func<Task>>?> _afterCommitCallbacks = new();

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

        public static IClientSessionHandle? Transaction => _currentSession.Value;
        public static bool IsInTransaction => Transaction != null && Transaction.IsInTransaction;

        public IClientSessionHandle SessionHandle { get; private set; }
        //public bool IsInTransaction => SessionHandle.IsInTransaction;

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

        public void AbortTransaction()
        {
            if (_isRootScope && SessionHandle.IsInTransaction)
                SessionHandle.AbortTransaction();
        }

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

                foreach (var callback in callbacks)
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
