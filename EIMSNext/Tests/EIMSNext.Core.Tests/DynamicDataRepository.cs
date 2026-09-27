using EIMSNext.Core.Repositories;
using EIMSNext.Persistence.PostgreSql;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 测试用表单数据仓储。
    /// </summary>
    public class FormDataRepository : DbRepository<FormData>
    {
        public FormDataRepository(TestPostgreSqlDbContext dbContext) : base(dbContext)
        {
        }
    }
}
