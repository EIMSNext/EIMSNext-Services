using EIMSNext.Core.Abstractions;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Tests;

/// <summary>
/// WorkflowCore 运行时 JSON 列（Data / PersistenceData / SubscriptionData 等）的序列化契约。
/// 列类型为 json（原样保留写入文本），因此序列化时 "$type" 必须恒为对象首键——
/// Newtonsoft 只把首键位的 $type 当类型元数据，一旦错位（如历史 jsonb 键序重排）整条读路径报废。
/// </summary>
[TestClass]
public sealed class WorkflowModelJsonTests
{
    [TestMethod]
    public void Data_RoundTrip_RestoresTypedNodeDatas()
    {
        var converter = GetDataConverter();
        var dataContext = new EfDataContext
        {
            CorpId = "c1",
            NodeDatas = new Dictionary<string, EfNodeData>
            {
                ["ins"] = new EfNodeData { NodeId = "ins", NodeExecResult = "08:00:00" },
                ["start"] = new EfNodeData { NodeId = "start" },
            },
        };

        var stored = (string)converter.ConvertToProvider(dataContext)!;
        var restored = Assert.IsInstanceOfType<EfDataContext>(converter.ConvertFromProvider(stored)!);
        Assert.AreEqual("ins", restored.NodeDatas["ins"].NodeId);
        Assert.AreEqual("08:00:00", restored.NodeDatas["ins"].NodeExecResult);
        Assert.AreEqual("start", restored.NodeDatas["start"].NodeId);
        Assert.AreEqual(2, restored.NodeDatas.Count);
    }

    [TestMethod]
    public void Data_Serialized_TypeMetadataIsAlwaysFirstKey()
    {
        // json 列按原文存储，键序 = 序列化输出键序。本断言锁死 "$type" 首键位不变量：
        // 若未来序列化实现破坏它，json 列里将出现读不回来的快照。
        var converter = GetDataConverter();
        var stored = (string)converter.ConvertToProvider(new EfDataContext
        {
            CorpId = "c1",
            Chain = new EfChainContext { Path = ["a", "b"] },
            NodeDatas = new Dictionary<string, EfNodeData> { ["ins"] = new() { NodeId = "ins" } },
        })!;

        var data = JObject.Parse(stored);
        Assert.AreEqual("$type", data.Properties().First().Name,
            "EfDataContext 序列化的 $type 必须是对象首键（json 列不重排键序）。");

        var nodeDatas = (JObject)data["NodeDatas"]!;
        foreach (var nodeProperty in nodeDatas.Properties().Where(p => p.Name != "$type"))
        {
            var node = (JObject)nodeProperty.Value;
            Assert.AreEqual("$type", node.Properties().First().Name,
                "EfNodeData 序列化的 $type 必须是对象首键。");
        }
    }

    private static ValueConverter GetDataConverter()
    {
        var builder = new ModelBuilder();
        builder.ConfigureWorkflowStore();
        var property = builder.Model.FindEntityType(typeof(WorkflowInstance))!
            .FindProperty(nameof(WorkflowInstance.Data))!;
        return property.GetValueConverter()!;
    }
}
