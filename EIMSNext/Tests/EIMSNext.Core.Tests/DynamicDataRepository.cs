using EIMSNext.Core.Repositories;
using EIMSNext.Persistence.PostgreSql;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 测试用表单数据仓储。
    /// <para>
    /// 迁移说明：<see cref="RepositoryBase{T}"/> 的构造参数从 Mongo 上下文改为 EF Core 的
    /// <see cref="DbContext"/>；<see cref="RepositoryBase{T}"/> 现在只承载
    /// 主键/基础查询语义，完整实现落在 <see cref="DbRepository{T}"/>，所以这里直接
    /// 派生 <see cref="DbRepository{T}"/>，避免再手抄 28 个抽象成员。
    /// 这里传 <see cref="TestPostgreSqlDbContext"/>：测试实体的模型只在它里面。
    /// </para>
    /// </summary>
    public class FormDataRepository : DbRepository<FormData>
    {
        public FormDataRepository(TestPostgreSqlDbContext dbContext) : base(dbContext)
        {
        }
    }
}
