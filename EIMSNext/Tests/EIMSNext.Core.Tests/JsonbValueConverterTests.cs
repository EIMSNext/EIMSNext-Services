using EIMSNext.Entities;
using EIMSNext.Persistence.PostgreSql;

namespace EIMSNext.Core.Tests;

[TestClass]
public sealed class JsonbValueConverterTests
{
    [TestMethod]
    public void PersistenceJson_KeepsFieldsHiddenOnlyFromHttpJson()
    {
        var form = new FormDef { Id = "f1", PublicRelatedFormIds = ["f2"] };
        var formConverter = JsonbValueConverter.Create<FormDef>();
        var formJson = formConverter.ConvertToProviderExpression.Compile()(form);

        StringAssert.Contains(formJson, "PublicRelatedFormIds");
        var formRoundTrip = formConverter.ConvertFromProviderExpression.Compile()(formJson);
        CollectionAssert.AreEqual(new[] { "f2" }, formRoundTrip.PublicRelatedFormIds);
    }
}
