using EIMSNext.Core.Repository;

namespace EIMSNext.Core.Test
{
    public class EntityDataRepository : RepositoryBase<EntityData>
    {
        public EntityDataRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
