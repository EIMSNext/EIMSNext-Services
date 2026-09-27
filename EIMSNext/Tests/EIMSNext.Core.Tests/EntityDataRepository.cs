using EIMSNext.Core.Repositories;
using EIMSNext.Persistence.PostgreSql;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 测试用仓储。
    /// </summary>
    public class EntityDataRepository : DbRepository<EntityData>
    {
        public EntityDataRepository(TestPostgreSqlDbContext dbContext) : base(dbContext)
        {
        }
    }
}
