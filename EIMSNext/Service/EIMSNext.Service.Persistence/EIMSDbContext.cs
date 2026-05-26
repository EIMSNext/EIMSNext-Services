using EIMSNext.MongoDb;

using Microsoft.Extensions.Options;

namespace EIMSNext.Service.Persistence
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
