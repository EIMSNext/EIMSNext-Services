using System.Dynamic;
using EIMSNext.Common.Extension;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;

using MongoDB.Driver;

namespace EIMSNext.Core.Test
{
    [TestClass]
    public class DynamicRepositoryTest : TestBase
    {
        [TestMethod]
        public void InsertTest()
        {
            var resp = new DynamicDataRepository(_dbContext!);

            var data = new DynamicData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"选项1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            data.UpdateTime = null;

            resp.Insert(data, _scope?.SessionHandle);

            var result = resp.Find(new DynamicFindOptions<DynamicData>() { Take = 10 }, _scope?.SessionHandle);
            Assert.AreEqual(1, result.CountDocuments());
            Assert.IsNull(result.First().UpdateTime);
        }

        [TestMethod]
        public void InsertManyTest()
        {
            var resp = new DynamicDataRepository(_dbContext!);

            var data1 = new DynamicData("{\"f_1721094301870\":\"aaa\",\"f_1722302387349\":\"选项1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1112\",\"f_1721094301877\": 2221},{\"f_1721094301876\": \"3331\",\"f_1721094301877\": 4441}]}");
            var data2 = new DynamicData("{\"f_1721094301870\":\"bbb\",\"f_1722302387349\":\"选项2\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2222},{\"f_1721094301876\": \"3332\",\"f_1721094301877\": 4442}]}");
            var data3 = new DynamicData("{\"f_1721094301870\":\"ccc\",\"f_1722302387349\":\"选项3\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2223},{\"f_1721094301876\": \"3333\",\"f_1721094301877\": 4443}]}");
            var datas = new List<DynamicData>
            {
                data1,
                data2,
                data3
            };

            resp.Insert(datas, _scope?.SessionHandle);

            var result = resp.Find(new DynamicFindOptions<DynamicData>() { Take = 10 }, _scope?.SessionHandle);
            Assert.AreEqual(3, result.CountDocuments());
        }

        [TestMethod]
        public void FindTest()
        {
            var data0= "{\"id\":\"\",\"appId\":\"68144ea3ee5f2aa4d37c02dd\",\"formId\":\"6877c3129839c3f592a87f98\",\"data\":{\"jxqoyp2pzq6orpoy\":\"1\",\"jiavqvqlmg0u3nmc\":\"1\",\"jkxo31f8215xjjel\":[{\"js0utbcbwu8qby53\":\"1\",\"jsne7rbcebsw3du0\":1},{\"js0utbcbwu8qby53\":\"2\",\"jsne7rbcebsw3du0\":2}]}}".DeserializeFromJson<DynamicData>();
          
            var resp = new DynamicDataRepository(_dbContext!);

            var data1 = new DynamicData("{\"f_1721094301870\":\"aaa\",\"f_1722302387349\":\"选项1\",\"f_1722302387351\":\"666-777\",\"f_1721094301874\": [{\"f_1721094301876\":\"1112\",\"f_1721094301877\": 2221},{\"f_1721094301876\": \"3331\",\"f_1721094301877\": 4441}]}");
            var data2 = new DynamicData("{\"f_1721094301870\":\"bbb\",\"f_1722302387349\":\"选项2\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2222},{\"f_1721094301876\": \"3332\",\"f_1721094301877\": 4442}]}");
            var data3 = new DynamicData("{\"f_1721094301870\":\"ccc\",\"f_1722302387349\":\"选项3\",\"f_1722302387351\":\"666-999\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2223},{\"f_1721094301876\": \"3333\",\"f_1721094301877\": 4443}]}");
            var datas = new List<DynamicData>
            {
                data1,
                data2,
                data3
            };

            resp.Insert(datas, _scope?.SessionHandle);

            var result = resp.Find(new DynamicFindOptions<DynamicData> { Filter = new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.Eq, Value = "bbb" } }, _scope?.SessionHandle);
            Assert.AreEqual(1, result.CountDocuments());

            result = resp.Find(new DynamicFindOptions<DynamicData> { Filter = new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.In, Value = new List<object> { "bbb", "ccc" } } }, _scope?.SessionHandle);
            Assert.AreEqual(2, result.CountDocuments());

            result = resp.Find(new DynamicFindOptions<DynamicData>
            {
                Filter = new DynamicFilter()
                {
                    Rel = FilterRel.Or,
                    Items = new List<DynamicFilter> {
                    new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.Eq, Value = "bbb" }, new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.Eq, Value = "ccc" }}
                }
            }, _scope?.SessionHandle);
            Assert.AreEqual(2, result.CountDocuments());

            result = resp.Find(new DynamicFindOptions<DynamicData> { Filter = new DynamicFilter { Items = new List<DynamicFilter> { new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.In, Value = new List<object> { "bbb", "ccc" } }, new DynamicFilter() { Items = new List<DynamicFilter> { new DynamicFilter { Field = "data.f_1722302387351", Op = FilterOp.Eq, Value = "666-999" } } } } } }, _scope?.SessionHandle);
            Assert.AreEqual(1, result.CountDocuments());

            result = resp.Find(new DynamicFindOptions<DynamicData> { Filter = new DynamicFilter { Field = "createTime", Op = FilterOp.Gt, Value = DateTime.Today.ToTimeStampMs() } }, _scope?.SessionHandle);
            Assert.AreEqual(3, result.CountDocuments());

            result = resp.Find(new DynamicFindOptions<DynamicData> { Filter = new DynamicFilter { Field = "data.f_1721094301874>f_1721094301876", Op = FilterOp.Eq, Value = "1113" } }, _scope?.SessionHandle);
            Assert.AreEqual(2, result.CountDocuments());
       
            result = resp.Find(new DynamicFindOptions<DynamicData> { Filter = new DynamicFilter { Field = "data.f_1721094301874>f_1721094301877", Op = FilterOp.Gt, Value = 4442 } }, _scope?.SessionHandle);
            Assert.AreEqual(1, result.CountDocuments());
        }

        [TestMethod]
        public void UpdateTest()
        {
            var resp = new DynamicDataRepository(_dbContext!);

            var data = new DynamicData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"选项1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            resp.Insert(data, _scope?.SessionHandle);

            var list = new List<UpdateDefinition<DynamicData>>();

            var innerData = new ExpandoObject();
            innerData.TryAdd("f_1721094301870", "fff");

            list.Add(resp.UpdateBuilder.Set(x => x.Data, innerData));
            list.Add(resp.UpdateBuilder.Set(x => x.CreateBy, new Operator("1", "1","001", "t1")));
            var udata = resp.UpdateBuilder.Combine(list);
            //new { Data = new { f_1721094301870 = "fff" }, _id = data.Id, CreateBy = new Entity.Operator { Id = "1", Code = "001", Name = "t1" } };
            resp.Update(data.Id, udata, session: _scope?.SessionHandle);

            var result = resp.Find(new DynamicFindOptions<DynamicData> { Filter = new DynamicFilter { Field = "createBy.empId", Op = FilterOp.Eq, Value = "001" } }, _scope?.SessionHandle);
            Assert.AreEqual(1, result.CountDocuments());
        }

        [TestMethod]
        public void UpdateManyTest()
        {
            var resp = new DynamicDataRepository(_dbContext!);

            var data1 = new DynamicData("{\"f_1721094301870\":\"aaa\",\"f_1722302387349\":\"选项1\",\"f_1722302387351\":\"666-777\",\"f_1721094301874\": [{\"f_1721094301876\":\"1112\",\"f_1721094301877\": 2221},{\"f_1721094301876\": \"3331\",\"f_1721094301877\": 4441}]}");
            var data2 = new DynamicData("{\"f_1721094301870\":\"bbb\",\"f_1722302387349\":\"选项2\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2222},{\"f_1721094301876\": \"3332\",\"f_1721094301877\": 4442}]}");
            var data3 = new DynamicData("{\"f_1721094301870\":\"ccc\",\"f_1722302387349\":\"选项3\",\"f_1722302387351\":\"666-999\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2223},{\"f_1721094301876\": \"3333\",\"f_1721094301877\": 4443}]}");
            var datas = new List<DynamicData>
            {
                data1,
                data2,
                data3
            };

            resp.Insert(datas, _scope?.SessionHandle);

            var filter = new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.In, Value = new List<object> { "bbb", "ccc" } };

            var list = new List<UpdateDefinition<DynamicData>>();

            var innerData = new ExpandoObject();
            innerData.TryAdd("f_1721094301870", "fff");

            list.Add(resp.UpdateBuilder.Set(x => x.Data, innerData));
            list.Add(resp.UpdateBuilder.Set(x => x.CreateBy, new Operator("1", "1", "001", "t1")));
            var udata = resp.UpdateBuilder.Combine(list);
            //new { Data = new { f_1721094301870 = "fff" }, _id = data.Id, CreateBy = new Entity.Operator { Id = "1", Code = "001", Name = "t1" } };
            resp.UpdateMany(filter, udata, session: _scope?.SessionHandle);

            var result = resp.Find(new DynamicFindOptions<DynamicData> { Filter = new DynamicFilter { Field = "createBy.empId", Op = FilterOp.Eq, Value = "001" } }, _scope?.SessionHandle);
            Assert.AreEqual(2, result.CountDocuments());
        }

        [TestMethod]
        public void ReplaceTest()
        {
            var resp = new DynamicDataRepository(_dbContext!);

            var data = new DynamicData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"选项1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            resp.Insert(data, _scope?.SessionHandle);

            var data2 = new DynamicData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"选项1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            data2.Id = data.Id;
            data2.CreateBy = new Operator("1", "1", "001", "t1");

            resp.Replace(data2, _scope?.SessionHandle);

            var result = resp.Find(new DynamicFindOptions<DynamicData> { Filter = new DynamicFilter { Field = "createBy.EmpId", Op = FilterOp.Eq, Value = "001" } }, _scope?.SessionHandle);
            Assert.AreEqual(1, result.CountDocuments());
        }

        [TestMethod]
        public void DeleteTest()
        {
            var resp = new DynamicDataRepository(_dbContext!);

            var data = new DynamicData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"选项1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            resp.Insert(data, _scope?.SessionHandle);
            _scope?.SessionHandle?.CommitTransaction();

            var result = resp.Find(new DynamicFindOptions<DynamicData>());
            Assert.AreEqual(1, result.CountDocuments());

            resp.Delete(data.Id);
            result = resp.Find(new DynamicFindOptions<DynamicData>());
            Assert.AreEqual(0, result.CountDocuments());
        }

        [TestMethod]
        public void DeleteManyTest()
        {
            var resp = new DynamicDataRepository(_dbContext!);

            var data1 = new DynamicData("{\"f_1721094301870\":\"aaa\",\"f_1722302387349\":\"选项1\",\"f_1722302387351\":\"666-777\",\"f_1721094301874\": [{\"f_1721094301876\":\"1112\",\"f_1721094301877\": 2221},{\"f_1721094301876\": \"3331\",\"f_1721094301877\": 4441}]}");
            var data2 = new DynamicData("{\"f_1721094301870\":\"bbb\",\"f_1722302387349\":\"选项2\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2222},{\"f_1721094301876\": \"3332\",\"f_1721094301877\": 4442}]}");
            var data3 = new DynamicData("{\"f_1721094301870\":\"ccc\",\"f_1722302387349\":\"选项3\",\"f_1722302387351\":\"666-999\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2223},{\"f_1721094301876\": \"3333\",\"f_1721094301877\": 4443}]}");
            var datas = new List<DynamicData>
            {
                data1,
                data2,
                data3
            };

            resp.Insert(datas, _scope?.SessionHandle);
            _scope?.SessionHandle?.CommitTransaction();

            var result = resp.Find(new DynamicFindOptions<DynamicData>());
            Assert.AreEqual(3, result.CountDocuments());

            resp.Delete(new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.In, Value = new List<object> { "bbb", "ccc" } });
            result = resp.Find(new DynamicFindOptions<DynamicData>());
            Assert.AreEqual(1, result.CountDocuments());

            resp.Delete(new DynamicFilter());
            result = resp.Find(new DynamicFindOptions<DynamicData>());
            Assert.AreEqual(0, result.CountDocuments());
        }
    }
}