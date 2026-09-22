using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 仓储读写测试。
    /// <para>
    /// 迁移说明：原实现用 <c>Insert(data, session)</c> + <c>Find(...).CountDocuments()</c>
    /// 这套 Mongo 会话 API；EF Core 下事务由 <c>TransactionScope</c> 隐式承载，仓储调用不再带
    /// session，计数改用 LINQ <c>Count()</c>。
    /// </para>
    /// </summary>
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
             new FieldDef{ Id="field_1", Label="field_111", Type= FieldType.Input}, new FieldDef{ Id="field_2", Label="field_222", Type= FieldType.Select1 } }
            };

            resp.Insert(data);

            // 过滤值先在内存里算好再塞进 DynamicFilter：DynamicFilter 的 Value 是普通对象，
            // 由表达式构造器读取，不参与 EF 的 SQL 翻译。
            var todayStart = DateTime.Today.ToTimeStampMs();

            var result = resp.Find(new DynamicFindOptions<EntityData> { Filter = new DynamicFilter { Field = "createTime", Op = FilterOp.Gt, Value = todayStart } });
            Assert.AreEqual(1, result.Count());

            _scope?.CommitTransaction();

            // 迁移说明：这里不能把 DateTime.Today.ToTimeStampMs() 直接写进 Where，
            // 那是自定义扩展方法，EF Core 无法翻译（会报 could not be translated）。
            // 先求值成局部变量，表达式里只剩常量比较。
            var cnt = resp.Queryable.Where(x => x.CreateTime > todayStart).Count();
            Assert.AreEqual(1, cnt);

            resp.Delete(data.Id);
            cnt = resp.Queryable.Count();
            Assert.AreEqual(0, cnt);
        }
    }
}
