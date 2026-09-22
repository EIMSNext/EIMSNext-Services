using EIMSNext.Common;
using EIMSNext.Core.Query;
using EIMSNext.Entities;

namespace EIMSNext.Core.Tests;

[TestClass]
public sealed class DynamicFilterValidationTests
{
    [TestMethod]
    public void NullEqualityValueBuildsNullPredicate()
    {
        var filter = new DynamicFilter { Field = "data.name", Op = FilterOp.Eq, Value = null };

        var predicate = filter.ToPredicate<FormData>();
        Assert.IsNotNull(predicate);
    }

    [TestMethod]
    public void AllInWithArrayValueBuildsPredicate()
    {
        var filter = new DynamicFilter { Field = "data.items", Op = FilterOp.AllIn, Value = new[] { "x" } };

        var predicate = filter.ToPredicate<FormData>();
        Assert.IsNotNull(predicate);

        var unknown = new DynamicFilter { Field = "data.items", Op = "elemmatch", Value = "x" };
        Assert.ThrowsExactly<BadRequestException>(() => unknown.ToPredicate<FormData>());
    }

    [TestMethod]
    public void MissingOperatorIsRejectedInsteadOfBecomingMatchAll()
    {
        var filter = new DynamicFilter { Field = "data.name", Value = "x" };

        Assert.ThrowsExactly<BadRequestException>(() => filter.ToPredicate<FormData>());
    }
}
