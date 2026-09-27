using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;

namespace EIMSNext.Core.Tests
{
    [TestClass]
    public class DynamicFindOptionsBehaviorTests
    {
        [TestMethod]
        public void GetEffectiveTake_UsesDefaultForNonPositiveTake()
        {
            var zero = new DynamicFindOptions<object> { Take = 0 };
            var negative = new DynamicFindOptions<object> { Take = -1 };

            Assert.AreEqual(200, zero.GetEffectiveTake());
            Assert.AreEqual(200, negative.GetEffectiveTake());
            Assert.AreEqual(25, new DynamicFindOptions<object> { Take = 25 }.GetEffectiveTake());
            Assert.AreEqual(200, new MongoFindOptions<object> { Take = 0 }.GetEffectiveTake());
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
