using Microsoft.VisualStudio.TestTools.UnitTesting;
using EIMSNext.Core.Mongo;

// These tests share the EIMSTest database and clear fixed collections in their setup.
[assembly: DoNotParallelize]

namespace EIMSNext.Core.Tests
{
    [TestClass]
    public sealed class TestAssemblyInitializer
    {
        [AssemblyInitialize]
        public static void Initialize(TestContext _)
        {
            MongoDatabase.RegisterConventions();
            MongoDatabase.RegisterSerializers();
        }
    }
}
