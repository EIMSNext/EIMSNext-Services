using EIMSNext.Core.Query;

namespace EIMSNext.Core.Tests
{
    [TestClass]
    public class DynamicFindOptionsBehaviorTests
    {
        [TestMethod]
        public void GetEffectiveTake_LeavesNonPositiveTakeAsUnlimited()
        {
            var zero = new DynamicFindOptions<object> { Take = 0 };
            var negative = new DynamicFindOptions<object> { Take = -1 };

            // 仓储层 0/负数 = 不限量（不生成 LIMIT）；请求入口的默认值与上限由 RequestPagingPolicy 负责。
            Assert.AreEqual(0, zero.GetEffectiveTake());
            Assert.AreEqual(0, negative.GetEffectiveTake());
            Assert.AreEqual(25, new DynamicFindOptions<object> { Take = 25 }.GetEffectiveTake());
            // 其 EF Core 等价物是 QueryFindOptions<T>（同一套 Take 语义）。
            Assert.AreEqual(0, new QueryFindOptions<object> { Take = 0 }.GetEffectiveTake());
        }

        [TestMethod]
        public void RequestPagingPolicy_NormalizesTakeIntoRange()
        {
            Assert.AreEqual(RequestPagingPolicy.DefaultTake, RequestPagingPolicy.Normalize(0));
            Assert.AreEqual(RequestPagingPolicy.DefaultTake, RequestPagingPolicy.Normalize(-5));
            Assert.AreEqual(50, RequestPagingPolicy.Normalize(50));
            Assert.AreEqual(RequestPagingPolicy.MaxTake, RequestPagingPolicy.Normalize(int.MaxValue));
            Assert.AreEqual(RequestPagingPolicy.PublicMaxTake, RequestPagingPolicy.Normalize(int.MaxValue, RequestPagingPolicy.PublicMaxTake));
        }

        [TestMethod]
        public void And_ComposesExistingAndAdditionalFilters()
        {
            var first = new DynamicFilter { Field = "corpId", Op = FilterOp.Eq, Value = "corp-1" };
            var combined = first.And("deleteFlag", FilterOp.Ne, true);

            Assert.IsNotNull(combined);
            Assert.AreEqual(FilterRel.And, combined.Rel);
            Assert.HasCount(2, combined.Items!);
            Assert.AreSame(first, combined.Items![0]);
            Assert.AreEqual("deleteFlag", combined.Items![1].Field);
        }
    }
}
