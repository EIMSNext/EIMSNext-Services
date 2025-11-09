using EIMSNext.MongoDb;
using Microsoft.Extensions.Options;

namespace EIMSNext.FileUpload
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