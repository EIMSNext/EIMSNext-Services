using EIMSNext.Core.Mongo;
using Microsoft.Extensions.Options;

namespace EIMSNext.File
{
    public class UploadDbContext : MongoDbContextBase
    {
        #region Variables

        #endregion

        public UploadDbContext(IOptions<MongoDbConfiguration> settings) : base(settings)
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
