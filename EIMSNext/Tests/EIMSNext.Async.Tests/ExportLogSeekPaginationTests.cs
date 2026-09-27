using EIMSNext.Async.Tasks.Export;
using EIMSNext.Core.Entities;

namespace EIMSNext.Async.Tests
{
    /// <summary>
    /// 导出 seek 分页过滤条件的单元测试。
    /// </summary>
    [TestClass]
    public class ExportLogSeekPaginationTests
    {
        [TestMethod]
        public void BuildSeekFilter_ShouldReturnBaseFilter_WhenCursorIsMissing()
        {
            System.Linq.Expressions.Expression<Func<TestEntity, bool>> baseFilter = x => !x.DeleteFlag;

            var filter = ExportProcessorBase.BuildSeekFilter(baseFilter, null, null);
            var rendered = filter.ToString();

            Assert.AreSame(baseFilter, filter, "游标缺失时应原样返回基础过滤条件。");
            StringAssert.Contains(rendered, "DeleteFlag");
            Assert.IsFalse(rendered.Contains("CreateTime", StringComparison.Ordinal), "游标缺失时不应出现 seek 条件。");
            Assert.IsFalse(rendered.Contains("Id", StringComparison.Ordinal), "游标缺失时不应出现 seek 条件。");
        }

        /// <summary>
        /// 验证 seek 分页条件在基础条件之上叠加，且语义为
        /// <c>CreateTime &lt; cursor || (CreateTime == cursor &amp;&amp; Id &lt; cursorId)</c>。
        /// <para>
        /// 注意：这里刻意<b>不做</b> <c>ToString()</c> 的字面量匹配。EF Core 10 会把
        /// <c>createTime</c>/<c>id</c> 这类局部变量编译成闭包字段访问，表达式渲染结果是
        /// <c>value(命名空间+&lt;&gt;c__DisplayClassX).createTime</c> 这种字段名，
        /// 不会再出现 <c>"1000"</c>/<c>"id-002"</c> 字面量，因此对字面量断言是脆弱的。
        /// 直接把表达式编译成委托并按边界样本求值，断言的就是真实运行语义
        /// （EF 最终翻译 SQL 时该表达式即为被翻译对象，语义等价）。
        /// </para>
        /// </summary>
        [TestMethod]
        public void BuildSeekFilter_ShouldUseCreateTimeAndIdAsSeekCursor()
        {
            System.Linq.Expressions.Expression<Func<TestEntity, bool>> baseFilter = x => !x.DeleteFlag;

            var filter = ExportProcessorBase.BuildSeekFilter(baseFilter, 1000L, "id-002");
            var rendered = filter.ToString();

            // 结构断言：seek 条件叠在基础条件上（AndAlso），seek 内部为 OR 组合（OrElse）。
            StringAssert.Contains(rendered, "DeleteFlag");
            StringAssert.Contains(rendered, "CreateTime");
            StringAssert.Contains(rendered, "Id");
            StringAssert.Contains(rendered, "OrElse", "seek 条件应为 OR 组合。");
            StringAssert.Contains(rendered, "AndAlso", "seek 条件应叠加在基础条件之上。");

            // 语义断言：编译表达式并按 keyset 边界样本求值。
            var predicate = filter.Compile();

            // CreateTime < 游标 -> 命中。
            Assert.IsTrue(Matches(predicate, createTime: 999, id: "zzz"),
                "CreateTime 小于游标时应命中。");
            // CreateTime == 游标 且 Id < 游标 Id -> 命中。
            Assert.IsTrue(Matches(predicate, createTime: 1000, id: "id-001"),
                "同 CreateTime 且 Id 小于游标 Id 时应命中。");
            // CreateTime == 游标 且 Id == / > 游标 Id -> 不命中（避免重复导出游标行本身）。
            Assert.IsFalse(Matches(predicate, createTime: 1000, id: "id-002"),
                "游标行本身不应再次命中。");
            Assert.IsFalse(Matches(predicate, createTime: 1000, id: "id-003"),
                "同 CreateTime 且 Id 大于游标 Id 时不应命中。");
            // CreateTime > 游标 -> 不命中（desc 排序下属于已导出区间）。
            Assert.IsFalse(Matches(predicate, createTime: 1001, id: "aaa"),
                "CreateTime 大于游标时不应命中。");
            // 已逻辑删除 -> 不命中（基础条件仍生效）。
            Assert.IsFalse(Matches(predicate, createTime: 999, id: "aaa", deleteFlag: true),
                "seek 条件不应绕过基础过滤条件。");
        }

        private static bool Matches(
            Func<TestEntity, bool> predicate,
            long createTime,
            string id,
            bool deleteFlag = false)
        {
            return predicate(new TestEntity { DeleteFlag = deleteFlag, CreateTime = createTime, Id = id });
        }

        private sealed class TestEntity : EntityBase
        {
        }
    }
}
