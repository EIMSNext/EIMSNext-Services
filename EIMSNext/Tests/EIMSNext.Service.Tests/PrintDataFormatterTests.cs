using System.Dynamic;
using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Entities;

namespace EIMSNext.Service.Tests;

[TestClass]
public class PrintDataFormatterTests
{
    [TestMethod]
    public void Format_ShouldUseLatestRoundPerApprovalNode()
    {
        var data = new FormData
        {
            Id = "data-1",
            FormId = "form-1",
            FlowStatus = FlowStatus.Approved,
            Data = new ExpandoObject(),
        };
        var logs = new[]
        {
            CreateLog("node-a", "A", 1, 100, "old"),
            CreateLog("node-a", "A", 2, 200, "latest"),
            CreateLog("node-b", "B", 1, 300, "B latest"),
            new Wf_TaskLog { NodeType = WfNodeType.CopyTo, NodeId = "node-c", Round = 9, ApprovalTime = 400 },
        };

        var result = (IDictionary<string, object?>)PrintDataFormatter.Format(data, Array.Empty<FieldDef>(), logs);
        var approvalLogs = (List<ExpandoObject>)result[PrintDataFormatter.ApprovalLogs]!;

        Assert.AreEqual(2, approvalLogs.Count);
        Assert.AreEqual("已审批", valuesFor(result, "flowStatus"));
        Assert.IsFalse(result.ContainsKey("flowstatus"));
        var values = approvalLogs
            .Select(item => (IDictionary<string, object?>)item)
            .ToList();
        CollectionAssert.AreEquivalent(new[] { "latest", "B latest" }, values.Select(item => item["comment"]?.ToString()).ToArray());
        Assert.IsFalse(values.Any(item => item["comment"]?.ToString() == "old"));
        Assert.IsTrue(values.All(item => item.ContainsKey("approvalTime") && item.ContainsKey("nodeName")));
        Assert.IsTrue(values.All(item => !item.ContainsKey("approvaltime") && !item.ContainsKey("nodename")));
    }

    private static string? valuesFor(IDictionary<string, object?> result, string key)
    {
        return result.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static Wf_TaskLog CreateLog(string nodeId, string nodeName, int round, long approvalTime, string comment)
    {
        return new Wf_TaskLog
        {
            NodeId = nodeId,
            NodeName = nodeName,
            NodeType = WfNodeType.Approve,
            Round = round,
            ApprovalTime = approvalTime,
            Comment = comment,
            Result = ApproveAction.Approve,
        };
    }
}
