using EIMSNext.Core.Repositories;

namespace EIMSNext.Core.Tests
{
    public class EntityDataRepository : RepositoryBase<EntityData>
    {
        public EntityDataRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
