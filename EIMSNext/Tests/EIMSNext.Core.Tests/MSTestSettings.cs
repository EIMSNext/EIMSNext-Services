using Microsoft.VisualStudio.TestTools.UnitTesting;

// These tests share the EIMSTest database and clear fixed tables in their setup.
[assembly: DoNotParallelize]

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 测试程序集初始化。
    /// <para>
    /// 迁移说明：原实现调用 <c>MongoDatabase.RegisterConventions()</c> /
    /// <c>RegisterSerializers()</c> 注册 BSON 序列化约定。PostgreSQL 侧不存在这套全局注册，
    /// EF Core 的映射在 <c>PostgreSqlDbContext.OnModelCreating</c> 里完成，因此本初始化器
    /// 不再需要任何操作，仅保留 <see cref="DoNotParallelizeAttribute"/> 语义（测试共用同一测试库）。
    /// </para>
    /// </summary>
    [TestClass]
    public sealed class TestAssemblyInitializer
    {
        [AssemblyInitialize]
        public static void Initialize(TestContext _)
        {
            // PostgreSQL 无需全局序列化注册；保留此钩子以便将来补充数据库就绪检查。
        }
    }
}
