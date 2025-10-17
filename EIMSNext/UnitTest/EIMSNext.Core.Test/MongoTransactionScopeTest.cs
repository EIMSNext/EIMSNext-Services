using EIMSNext.Common;
using EIMSNext.Core.MongoDb;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Test
{
    [TestClass]
    public class MongoTransactionScopeTest
    {
        protected DbContext? _dbContext;

        [TestMethod]
        public void ThreadTransactionTest()
        {
            using (var scope = new MongoTransactionScope(_dbContext!))
            {
                using (ExecutionContext.SuppressFlow())
                {
                    Task.Run(() =>
                    {
                        var t = MongoTransactionScope.Transaction;
                        Assert.IsNull(t);
                    });
                }

                Thread.Sleep(1000);

                Assert.IsTrue(MongoTransactionScope.IsInTransaction);
                Thread.Sleep(1000);
                Assert.IsTrue(MongoTransactionScope.IsInTransaction);
            }

            Assert.IsNull(MongoTransactionScope.Transaction);
        }

        [TestMethod]
        public void AutoInTransactionTest()
        {
            using (var scope = new MongoTransactionScope(_dbContext!))
            {
                var resp = new EntityDataRepository(_dbContext!);

                var data = new EntityData()
                {
                    AppId = "111",
                    AppName = "TestApp",
                    Fields = new FieldDefList {
                    new FieldDef{ Id="field_1", Label="field_111", Type= FieldType.Input}, new FieldDef{ Id="field_2", Label="field_222", Type= FieldType.Select } }
                };

                resp.Insert(data);

                var result = resp.Find(new DynamicFindOptions<EntityData> { Filter = new DynamicFilter { Field = "CreateTime", Op = FilterOp.Gt, Value = DateTime.Today } });
                Assert.AreEqual(1, result.CountDocuments());

                scope?.CommitTransaction();
                //Query不能执行事务内查询
                var cnt = resp.Queryable.Where(x => x.CreateTime > DateTime.Today).Count();
                Assert.AreEqual(1, cnt);

                scope?.AbortTransaction();
            }
        }

        [TestInitialize]
        public void Init()
        {
            _dbContext = DbContext.Create();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext?.Dispose();
        }
    }
}
