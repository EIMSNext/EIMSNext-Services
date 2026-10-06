using System.Dynamic;
using System.Globalization;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Tests
{
    [TestClass]
    public class DynamicRepositoryTest : TestBase
    {
        [TestMethod]
        public void InsertTest()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data = new FormData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"ѡ��1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            data.UpdateTime = null;

            resp.Insert(data);

            var result = resp.Find(new DynamicFindOptions<FormData>() { Take = 10 }).ToList();
            Assert.AreEqual(1, result.Count);
            Assert.IsNull(result.First().UpdateTime);
        }

        [TestMethod]
        public void InsertManyTest()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data1 = new FormData("{\"f_1721094301870\":\"aaa\",\"f_1722302387349\":\"ѡ��1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1112\",\"f_1721094301877\": 2221},{\"f_1721094301876\": \"3331\",\"f_1721094301877\": 4441}]}");
            var data2 = new FormData("{\"f_1721094301870\":\"bbb\",\"f_1722302387349\":\"ѡ��2\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2222},{\"f_1721094301876\": \"3332\",\"f_1721094301877\": 4442}]}");
            var data3 = new FormData("{\"f_1721094301870\":\"ccc\",\"f_1722302387349\":\"ѡ��3\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2223},{\"f_1721094301876\": \"3333\",\"f_1721094301877\": 4443}]}");
            var datas = new List<FormData>
            {
                data1,
                data2,
                data3
            };

            resp.Insert(datas);

            var result = resp.Find(new DynamicFindOptions<FormData>() { Take = 10 }).ToList();
            Assert.AreEqual(3, result.Count);
        }

        [TestMethod]
        public void FindTest()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data1 = new FormData("{\"f_1721094301870\":\"aaa\",\"f_1722302387349\":\"ѡ��1\",\"f_1722302387351\":\"666-777\",\"f_1721094301874\": [{\"f_1721094301876\":\"1112\",\"f_1721094301877\": 2221},{\"f_1721094301876\": \"3331\",\"f_1721094301877\": 4441}]}");
            var data2 = new FormData("{\"f_1721094301870\":\"bbb\",\"f_1722302387349\":\"ѡ��2\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2222},{\"f_1721094301876\": \"3332\",\"f_1721094301877\": 4442}]}");
            var data3 = new FormData("{\"f_1721094301870\":\"ccc\",\"f_1722302387349\":\"ѡ��3\",\"f_1722302387351\":\"666-999\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2223},{\"f_1721094301876\": \"3333\",\"f_1721094301877\": 4443}]}");
            var datas = new List<FormData>
            {
                data1,
                data2,
                data3
            };

            resp.Insert(datas);

            var result = resp.Find(new DynamicFindOptions<FormData> { Filter = new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.Eq, Value = "bbb" } }).ToList();
            Assert.AreEqual(1, result.Count);

            result = resp.Find(new DynamicFindOptions<FormData> { Filter = new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.In, Value = new List<object> { "bbb", "ccc" } } }).ToList();
            Assert.AreEqual(2, result.Count);

            result = resp.Find(new DynamicFindOptions<FormData>
            {
                Filter = new DynamicFilter()
                {
                    Rel = FilterRel.Or,
                    Items = new List<DynamicFilter> {
                    new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.Eq, Value = "bbb" }, new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.Eq, Value = "ccc" }}
                }
            }).ToList();
            Assert.AreEqual(2, result.Count);

            result = resp.Find(new DynamicFindOptions<FormData> { Filter = new DynamicFilter { Items = new List<DynamicFilter> { new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.In, Value = new List<object> { "bbb", "ccc" } }, new DynamicFilter() { Items = new List<DynamicFilter> { new DynamicFilter { Field = "data.f_1722302387351", Op = FilterOp.Eq, Value = "666-999" } } } } } }).ToList();
            Assert.AreEqual(1, result.Count);

            result = resp.Find(new DynamicFindOptions<FormData> { Filter = new DynamicFilter { Field = "createTime", Op = FilterOp.Gt, Value = DateTime.Today.ToTimeStampMs() } }).ToList();
            Assert.AreEqual(3, result.Count);

            result = resp.Find(new DynamicFindOptions<FormData> { Filter = new DynamicFilter { Field = "data.f_1721094301874>f_1721094301876", Op = FilterOp.Eq, Value = "1113" } }).ToList();
            Assert.AreEqual(2, result.Count);

            result = resp.Find(new DynamicFindOptions<FormData> { Filter = new DynamicFilter { Field = "data.f_1721094301874>f_1721094301877", Op = FilterOp.Gt, Value = 4442 } }).ToList();
            Assert.AreEqual(1, result.Count);
        }

        /// <summary>
        /// jsonb 内部键排序必须按值类型比较，而不是文本序。
        /// </summary>
        /// <remarks>
        /// 回归用例：排序键若走 <c>eims_json_text</c>（返回 text），字典序会把 <c>"10"</c> 排在
        /// <c>"9"</c> 前面，得到 10, 100, 2, 9；现在走 <c>eims_json_sort</c>（返回 jsonb），
        /// 数字按数值序。同时覆盖「字段缺失」的落位：jsonb 的 <c>'null'</c> 是最小一类，
        /// </remarks>
        [TestMethod]
        public void SortByJsonbNumericFieldTest()
        {
            var resp = new FormDataRepository(_dbContext!);

            resp.Insert(new List<FormData>
            {
                new("{\"f_score\": 9}"),
                new("{\"f_score\": 10}"),
                new("{\"f_score\": 100}"),
                new("{\"f_score\": 2}"),
                new("{\"f_other\": 1}")
            });

            static long? Score(FormData x)
                => ((IDictionary<string, object?>)x.Data).TryGetValue("f_score", out var value) && value is not null
                    ? long.Parse(value.ToString()!, CultureInfo.InvariantCulture)
                    : null;

            var ascendingQuery = resp.Find(new DynamicFindOptions<FormData>
            {
                Sort = new DynamicSortList { new DynamicSort { Field = "data.f_score", Dir = SortDir.Asc } },
                Take = 10
            });

            // 防回归：排序键必须落到返回 jsonb 的函数上。若将来有人把 OrderBy 改回 BuildBody，
            // 这里会退化成 eims_json_text（text 字典序），断言先失败、不必等到上面的顺序断言。
            StringAssert.Contains(
                ascendingQuery.ToQueryString(), "eims_json_sort",
                "jsonb 路径排序应使用 eims_json_sort（返回 jsonb），而不是 eims_json_text。");

            var ascending = ascendingQuery.AsEnumerable().Select(Score).ToList();

            CollectionAssert.AreEqual(
                new long?[] { null, 2, 9, 10, 100 }, ascending,
                "升序：缺失字段排最前，数字按数值序而非字典序。");

            var descending = resp.Find(new DynamicFindOptions<FormData>
            {
                Sort = new DynamicSortList { new DynamicSort { Field = "data.f_score", Dir = SortDir.Desc } },
                Take = 10
            }).AsEnumerable().Select(Score).ToList();

            CollectionAssert.AreEqual(
                new long?[] { 100, 10, 9, 2, null }, descending,
                "降序：缺失字段排最后。");
        }

        [TestMethod]
        public void UpdateTest()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data = new FormData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"ѡ��1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            resp.Insert(data);

            var innerData = new Dictionary<string, object?>();
            innerData.TryAdd("f_1721094301870", "fff");

            resp.UpdateMany(x => x.Id == data.Id, setters => setters
                .SetProperty(x => x.Data, innerData)
                .SetProperty(x => x.CreateBy, new Operator("1", "001", "t1")));

            var result = resp.Find(new DynamicFindOptions<FormData> { Filter = new DynamicFilter { Field = "createBy.value", Op = FilterOp.Eq, Value = "001" } }).ToList();
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task UpdateManyTest()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data1 = new FormData("{\"f_1721094301870\":\"aaa\",\"f_1722302387349\":\"ѡ��1\",\"f_1722302387351\":\"666-777\",\"f_1721094301874\": [{\"f_1721094301876\":\"1112\",\"f_1721094301877\": 2221},{\"f_1721094301876\": \"3331\",\"f_1721094301877\": 4441}]}");
            var data2 = new FormData("{\"f_1721094301870\":\"bbb\",\"f_1722302387349\":\"ѡ��2\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2222},{\"f_1721094301876\": \"3332\",\"f_1721094301877\": 4442}]}");
            var data3 = new FormData("{\"f_1721094301870\":\"ccc\",\"f_1722302387349\":\"ѡ��3\",\"f_1722302387351\":\"666-999\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2223},{\"f_1721094301876\": \"3333\",\"f_1721094301877\": 4443}]}");
            var datas = new List<FormData>
            {
                data1,
                data2,
                data3
            };

            resp.Insert(datas);

            var filter = new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.In, Value = new List<object> { "bbb", "ccc" } };

            var innerData = new Dictionary<string, object?>();
            innerData.TryAdd("f_1721094301870", "fff");

            await resp.UpdateManyAsync(filter, setters => setters
                .SetProperty(x => x.Data, innerData)
                .SetProperty(x => x.CreateBy, new Operator("1", "001", "t1")));

            var result = resp.Find(new DynamicFindOptions<FormData> { Filter = new DynamicFilter { Field = "createBy.value", Op = FilterOp.Eq, Value = "001" } }).ToList();
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void ReplaceTest()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data = new FormData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"ѡ��1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            resp.Insert(data);

            var data2 = new FormData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"ѡ��1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            data2.Id = data.Id;
            data2.CreateBy = new Operator("1", "001", "t1");

            resp.Replace(data2);

            var result = resp.Find(new DynamicFindOptions<FormData> { Filter = new DynamicFilter { Field = "createBy.value", Op = FilterOp.Eq, Value = "001" } }).ToList();
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public void DeleteTest()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data = new FormData("{\"f_1721094301870\":\"fff\",\"f_1722302387349\":\"ѡ��1\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"111\",\"f_1721094301877\": 222},{\"f_1721094301876\": \"333\",\"f_1721094301877\": 444}]}");
            resp.Insert(data);

            var result = resp.Find(new DynamicFindOptions<FormData>()).ToList();
            Assert.AreEqual(1, result.Count);

            resp.Delete(data.Id);
            result = resp.Find(new DynamicFindOptions<FormData>()).ToList();
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task DeleteManyTest()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data1 = new FormData("{\"f_1721094301870\":\"aaa\",\"f_1722302387349\":\"ѡ��1\",\"f_1722302387351\":\"666-777\",\"f_1721094301874\": [{\"f_1721094301876\":\"1112\",\"f_1721094301877\": 2221},{\"f_1721094301876\": \"3331\",\"f_1721094301877\": 4441}]}");
            var data2 = new FormData("{\"f_1721094301870\":\"bbb\",\"f_1722302387349\":\"ѡ��2\",\"f_1722302387351\":\"666-888\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2222},{\"f_1721094301876\": \"3332\",\"f_1721094301877\": 4442}]}");
            var data3 = new FormData("{\"f_1721094301870\":\"ccc\",\"f_1722302387349\":\"ѡ��3\",\"f_1722302387351\":\"666-999\",\"f_1721094301874\": [{\"f_1721094301876\":\"1113\",\"f_1721094301877\": 2223},{\"f_1721094301876\": \"3333\",\"f_1721094301877\": 4443}]}");
            var datas = new List<FormData>
            {
                data1,
                data2,
                data3
            };

            resp.Insert(datas);

            var result = resp.Find(new DynamicFindOptions<FormData>()).ToList();
            Assert.AreEqual(3, result.Count);

            await resp.DeleteManyAsync(new DynamicFilter { Field = "data.f_1721094301870", Op = FilterOp.In, Value = new List<object> { "bbb", "ccc" } });
            result = resp.Find(new DynamicFindOptions<FormData>()).ToList();
            Assert.AreEqual(1, result.Count);

            await resp.DeleteManyAsync(new DynamicFilter());
            result = resp.Find(new DynamicFindOptions<FormData>()).ToList();
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>
        /// 软删除必须落 DeleteFlag 与删除人/删除时间，且这些值能从 jsonb 列正确往返。
        /// </summary>
        /// <remarks>
        /// 这条用例同时守两件事：
        /// 1) <c>ExecuteUpdateAsync</c> 能把带值转换器的 <see cref="Operator"/>（jsonb）写进库；
        /// 2) 软删除后的行在带全局 <c>!DeleteFlag</c> 过滤的正常查询里读不到。
        /// </remarks>
        [TestMethod]
        public void SoftDeleteFillsDeleteAuditFields()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data = new FormData("{\"f_1721094301870\":\"fff\"}");
            resp.Insert(data);

            var deleteBy = new Operator("emp-1", "E001", "张三");
            const long deleteTime = 1735689600000L;

            resp.SoftDeleteManyAsync([data.Id], deleteBy, deleteTime).GetAwaiter().GetResult();

            var row = resp.Find(new DynamicFindOptions<FormData> { IncludeDeleted = true })
                .Single(x => x.Id == data.Id);

            Assert.IsTrue(row.DeleteFlag);
            Assert.IsNotNull(row.DeleteBy);
            Assert.AreEqual("emp-1", row.DeleteBy!.Id);
            Assert.AreEqual("E001", row.DeleteBy.Value);
            Assert.AreEqual("张三", row.DeleteBy.Label);
            Assert.AreEqual(deleteTime, row.DeleteTime!.Value);

            // 带全局软删除过滤的常规查询不再返回该行。
            Assert.AreEqual(0, resp.Find(new DynamicFindOptions<FormData>()).Count());
        }

        /// <summary>
        /// 恢复（DeleteFlag=false）时必须把删除人/删除时间清空，避免已恢复的行仍残留删除痕迹。
        /// </summary>
        /// <remarks>
        /// 这里刻意复刻 <c>FormDataService.RestoreAsync</c> 的写法：全局软删除过滤会把已删行排除，
        /// 所以必须先 <c>IgnoreQueryFilters()</c>；同时验证 <c>null</c> 常量能被写进带值转换器的
        /// jsonb 列（<see cref="Operator"/>）。
        /// </remarks>
        [TestMethod]
        public void RestoreClearsDeleteAuditFields()
        {
            var resp = new FormDataRepository(_dbContext!);

            var data = new FormData("{\"f_1721094301870\":\"fff\"}");
            resp.Insert(data);

            resp.SoftDeleteManyAsync([data.Id], new Operator("emp-1", "E001", "张三"), 1735689600000L)
                .GetAwaiter().GetResult();

            Operator? clearedBy = null;
            long? clearedTime = null;
            var updated = resp.Queryable
                .IgnoreQueryFilters()
                .Where(x => x.Id == data.Id && x.DeleteFlag)
                .ExecuteUpdate(setters => setters
                    .SetProperty(x => x.DeleteFlag, false)
                    .SetProperty(x => x.DeleteBy, clearedBy)
                    .SetProperty(x => x.DeleteTime, clearedTime));

            Assert.AreEqual(1, updated);

            var row = resp.Find(new DynamicFindOptions<FormData>()).Single(x => x.Id == data.Id);
            Assert.IsFalse(row.DeleteFlag);
            Assert.IsNull(row.DeleteBy);
            Assert.IsNull(row.DeleteTime);
        }
    }
}
