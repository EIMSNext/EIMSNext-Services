using EIMSNext.Persistence.PostgreSql;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 测试数据库上下文工厂。
    /// </summary>
    public static class TestDbFactory
    {
        /// <summary>测试库连接串；可通过环境变量覆盖。</summary>
        public static string ConnectionString =>
            Environment.GetEnvironmentVariable("EIMS_TEST_POSTGRES")
            ?? "Host=localhost;Port=5432;Database=EIMSTest;Username=postgres;Password=sa123";

        /// <summary>创建指向测试库的生产上下文。</summary>
        public static PostgreSqlDbContext Create()
        {
            var options = new DbContextOptionsBuilder<PostgreSqlDbContext>()
                .UseNpgsql(ConnectionString)
                .UseEimsJsonPathOperators()
                .Options;
            return new PostgreSqlDbContext(options);
        }

        /// <summary>创建只含测试自有实体的上下文。</summary>
        public static TestPostgreSqlDbContext CreateTest()
        {
            var options = new DbContextOptionsBuilder<TestPostgreSqlDbContext>()
                .UseNpgsql(ConnectionString)
                .UseEimsJsonPathOperators()
                .Options;
            return new TestPostgreSqlDbContext(options);
        }

        /// <summary>
        /// 重建测试自有实体对应的表。
        /// </summary>
        /// <remarks>
        /// DDL 由 <see cref="TestPostgreSqlDbContext"/> 的模型现场生成（<c>GenerateCreateScript</c>），
        /// 而不是手写一份并行维护的建表语句——这样模型与表结构不可能漂移。
        /// 只有本工程自己的 <c>TestFormData</c> / <c>TestEntityData</c> 会被创建，
        /// 测试库里的业务表不受影响。
        /// </remarks>
        public static void EnsureTestSchema()
        {
            using var db = CreateTest();

            db.Database.ExecuteSqlRaw(
                """
                drop table if exists "TestFormData" cascade;
                drop table if exists "TestEntityData" cascade;
                drop table if exists "TestFormDef" cascade;
                """);

            db.Database.ExecuteSqlRaw(db.Database.GenerateCreateScript());
        }
    }
}
