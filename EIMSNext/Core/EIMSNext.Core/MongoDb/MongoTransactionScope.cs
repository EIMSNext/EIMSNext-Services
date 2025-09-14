using EIMSNext.MongoDb;

using MongoDB.Driver;

namespace EIMSNext.Core.MongoDb
{
    public class MongoTransactionScope : IDisposable
    {
        private static readonly AsyncLocal<IClientSessionHandle?> _currentSession = new AsyncLocal<IClientSessionHandle?>();
        private bool _isRootScope;
        private bool _completed = false;

        public MongoTransactionScope(IMongoDbContex dbContex, TransactionOptions? transOptions = null)
        {
            if (_currentSession.Value == null)
            {
                var options = transOptions ?? new TransactionOptions(readConcern: ReadConcern.Majority, writeConcern: WriteConcern.WMajority);
                SessionHandle = dbContex.Database.Client.StartSession();
                SessionHandle.StartTransaction(options);

                _currentSession.Value = SessionHandle;
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
            }
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
                try
                {
                    if (!_completed)
                        AbortTransaction();
                }
                finally
                {
                    _currentSession.Value = null;
                    SessionHandle.Dispose();
                }
            }
        }
    }
}
