using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo;

namespace EIMSNext.Core.Mongo.Repositories
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
