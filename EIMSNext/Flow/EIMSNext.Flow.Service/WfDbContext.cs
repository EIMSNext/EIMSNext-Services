using EIMSNext.MongoDb;

using Microsoft.Extensions.Options;

namespace EIMSNext.Flow.Service
{
    public class WfDbContext : MongoDbContextBase
    {
        #region Variables

        #endregion

        public WfDbContext(IOptions<MongoDbConfiguration> settings) : base(settings)
        {
        }

        #region Properties

        #endregion

        #region Methods

        #endregion

        #region Helper       

        #endregion
    }
}