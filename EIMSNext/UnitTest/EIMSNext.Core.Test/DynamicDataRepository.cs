using EIMSNext.Core.Repository;

namespace EIMSNext.Core.Test
{
    public class DynamicDataRepository : RepositoryBase<DynamicData>
    {
        public DynamicDataRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }

    public class FormDataRepository : RepositoryBase<FormData>
    {
        public FormDataRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
