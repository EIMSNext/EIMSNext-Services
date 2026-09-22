using EIMSNext.Persistence.PostgreSql;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 测试数据库上下文工厂。
    /// <para>
    /// 迁移说明：原测试有一个继承 <c>MongoDbContextBase</c> 的 <c>DbContext</c> 类，直连
    /// localhost:27017 的 EIMSTest 库。PostgreSQL 迁移后：
    /// <list type="bullet">
    /// <item><description><see cref="Create"/> 返回生产上下文 <see cref="PostgreSqlDbContext"/>
    /// （它是 sealed，因此这里只提供工厂，不再派生子类），用于事务、表结构一致性等与真实业务表相关的用例；</description></item>
    /// <item><description><see cref="CreateTest"/> 返回只含测试自有实体的 <see cref="TestPostgreSqlDbContext"/>，
    /// 供 <c>DbRepository&lt;T&gt;</c> / 动态查询用例使用。</description></item>
    /// </list>
    /// 连接串可由环境变量 <c>EIMS_TEST_POSTGRES</c> 覆盖。
    /// </para>
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
