using EIMSNext.Core.Repositories;
using EIMSNext.Persistence.PostgreSql;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 测试用仓储。
    /// <para>
    /// 迁移说明：<see cref="RepositoryBase{T}"/> 的构造参数从 Mongo 上下文改为 EF Core 的
    /// <see cref="DbContext"/>；完整仓储实现落在 <see cref="DbRepository{T}"/>，
    /// 这里直接派生它，避免手抄全部抽象成员。上下文用
    /// <see cref="TestPostgreSqlDbContext"/>：<c>EntityData</c> 只在该模型里。
    /// </para>
    /// </summary>
    public class EntityDataRepository : DbRepository<EntityData>
    {
        public EntityDataRepository(TestPostgreSqlDbContext dbContext) : base(dbContext)
        {
        }
    }
}
