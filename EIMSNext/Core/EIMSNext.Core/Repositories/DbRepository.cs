using EIMSNext.Core.Entities;
using EIMSNext.MongoDb;

namespace EIMSNext.Core.Repositories
{
    public class DbRepository<T> : RepositoryBase<T> where T : class, IMongoEntity
    {
        #region Variables

        #endregion

        public DbRepository(IMongoDbContex dbContext)
            : base(dbContext)
        {
        }

        #region Properties

        #endregion

        #region Methods       

        #endregion

        #region Async Methods

        #endregion

        #region Helper

        #endregion
    }
}