using EIMSNext.Core.Query;

using MongoDB.Bson;

namespace EIMSNext.Core.Test
{
    [TestClass]
    public class EntityRepositoryTest : TestBase
    {
        [TestMethod]
        public void InsertTest()
        {
            var resp = new EntityDataRepository(_dbContext!);

            var data = new EntityData()
            {
                AppId = "111",
                AppName = "TestApp",
                Fields = new FieldDefList {
             new FieldDef{ Id="field_1", Label="field_111", Type= FieldType.Input}, new FieldDef{ Id="field_2", Label="field_222", Type= FieldType.Select } }
            };

            resp.Insert(data, _scope?.SessionHandle);

            var result = resp.Find(new DynamicFindOptions<EntityData> { Filter = new DynamicFilter { Field = "CreateTime", Op = FilterOp.Gt, Value = DateTime.Today } }, _scope?.SessionHandle);
            Assert.AreEqual(1, result.CountDocuments());

            _scope?.CommitTransaction();
            //Query不能执行事务内查询
            var cnt = resp.Queryable.Where(x => x.CreateTime > DateTime.Today).Count();
            Assert.AreEqual(1, cnt);

            resp.Delete(data.Id);
            cnt = resp.Queryable.Count();
            Assert.AreEqual(0, cnt);
        }
    }
}
