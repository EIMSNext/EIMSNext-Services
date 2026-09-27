using System.Dynamic;
using EIMSNext.Print.Extensions;

namespace EIMSNext.Print.Tests;

[TestClass]
public class JsonExtensionTests
{
    [TestMethod]
    public void ConvertToJsonObject_ShouldNormalizeTopLevelAndNestedKeysToLowerCase()
    {
        var approvalLog = new ExpandoObject();
        var approvalValues = (IDictionary<string, object?>)approvalLog;
        approvalValues["approvalTime"] = "2026-09-02 10:00:00";
        approvalValues["nodeName"] = "审批";

        var data = new ExpandoObject();
        var values = (IDictionary<string, object?>)data;
        values["flowStatus"] = "已审批";
        values["currentOwner"] = "张三";
        values["approvallogs"] = new List<ExpandoObject> { approvalLog };

        var result = new object[] { data }.ConvertToJsonObject().Single();

        Assert.AreEqual("已审批", result["flowstatus"]?.GetValue<string>());
        Assert.AreEqual("张三", result["currentowner"]?.GetValue<string>());
        Assert.IsFalse(result.ContainsKey("flowStatus"));
        var logs = result["approvallogs"]!.AsArray();
        var log = logs[0]!.AsObject();
        Assert.AreEqual("2026-09-02 10:00:00", log["approvaltime"]?.GetValue<string>());
        Assert.AreEqual("审批", log["nodename"]?.GetValue<string>());
        Assert.IsFalse(log.ContainsKey("approvalTime"));
        Assert.IsFalse(log.ContainsKey("nodeName"));
    }
}
