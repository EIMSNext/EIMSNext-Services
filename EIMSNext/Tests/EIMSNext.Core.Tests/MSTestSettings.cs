using Microsoft.VisualStudio.TestTools.UnitTesting;

// These tests share the EIMSTest database and clear fixed tables in their setup.
[assembly: DoNotParallelize]

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 测试程序集初始化。
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
