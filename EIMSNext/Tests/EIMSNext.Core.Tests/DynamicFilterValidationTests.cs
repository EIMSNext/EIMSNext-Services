using EIMSNext.Common;
using EIMSNext.Core.Query;
using EIMSNext.Entities;

namespace EIMSNext.Core.Tests;

[TestClass]
public sealed class DynamicFilterValidationTests
{
    [TestMethod]
    public void NullEqualityValueIsIgnored()
    {
        var filter = new DynamicFilter { Field = "data.name", Op = FilterOp.Eq, Value = null };

        var predicate = filter.ToPredicate<FormData>();
        Assert.IsNotNull(predicate);
        StringAssert.Contains(predicate.ToString(), "True");
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
    public void MissingOperatorIsIgnored()
    {
        var filter = new DynamicFilter { Field = "data.name", Value = "x" };

        var predicate = filter.ToPredicate<FormData>();
        StringAssert.Contains(predicate.ToString(), "True");
    }

    [TestMethod]
    public void EmptyAllInIsFalse()
    {
        var filter = new DynamicFilter { Field = "data.items", Op = FilterOp.AllIn, Value = Array.Empty<string>() };

        var predicate = filter.ToPredicate<FormData>();
        StringAssert.Contains(predicate.ToString(), "False");
    }

    [TestMethod]
    public void InWithoutValueIsIgnored()
    {
        var filter = new DynamicFilter { Field = "data.items", Op = FilterOp.In, Value = null };

        var predicate = filter.ToPredicate<FormData>();
        StringAssert.Contains(predicate.ToString(), "True");
    }

    [TestMethod]
    public void BetweenWithOneValueIsIgnored()
    {
        var filter = new DynamicFilter { Field = "data.amount", Op = FilterOp.Between, Value = new[] { 10 } };

        var predicate = filter.ToPredicate<FormData>();
        StringAssert.Contains(predicate.ToString(), "True");
    }
}
