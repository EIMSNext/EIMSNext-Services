using EIMSNext.Core.Mongo;

using Microsoft.Extensions.Options;

namespace EIMSNext.Persistence.Mongo
{
    public class EIMSDbContext : MongoDbContextBase
    {
        #region Variables

        #endregion

        public EIMSDbContext(IOptions<MongoDbConfiguration> settings) : base(settings)
        {
        }

        #region Properties

        #endregion

        #region Methods

        #endregion
    }
}
