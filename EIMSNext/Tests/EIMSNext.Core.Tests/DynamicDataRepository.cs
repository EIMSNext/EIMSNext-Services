using EIMSNext.Core.Mongo.Repositories;

namespace EIMSNext.Core.Tests
{
    public class FormDataRepository : RepositoryBase<FormData>
    {
        public FormDataRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
