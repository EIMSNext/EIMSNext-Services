using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;

using EIMSNext.Component;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Nodes;
using EIMSNext.Plugin.Contracts;
using EIMSNext.Scripting;
using EIMSNext.Entities;
using SamplePlugin;

namespace EIMSNext.Flow.Tests
{
    [TestClass]
    public class PluginNodeConfigToExecutionSimulationTests
    {
        [TestMethod]
        public void PluginNode_ConfigToExecution_BindsCustomValuesAndWritesResults()
        {
            TestJsonOptions.UseProjectDefaults();
            var pluginSetting = ParsePluginSetting(BuildWorkflowContent(useNumericStrings: false));

            var payload = InvokeBuildPayload(pluginSetting, CreateSimulationScriptEngine());
            using var plugin = new SimulationPlugin();
            var execResult = plugin.Execute(
                pluginSetting,
                new PluginExecArgs
                {
                    FunName = "Echo",
                    FunArgs = payload.SerializeToJson()
                });

            Assert.AreEqual(0, execResult.Code, execResult.Message);
            var result = (IDictionary<string, object?>)execResult.Result!;
            Assert.AreEqual("BIZ-001", result["echoBizNo"]);
            Assert.AreEqual(12.34m, result["echoAmount"]);
            Assert.AreEqual(1710000000000L, result["echoBizDate"]);
            Assert.AreEqual("paid", result["echoStatus"]);
            CollectionAssert.AreEqual(new[] { "urgent", "finance" }, ((List<string>)result["echoTags"]!).ToArray());
            Assert.AreEqual("u1", ((EmployeeRef)result["echoOwner"]!).Id);
            Assert.AreEqual(2, ((List<EmployeeRef>)result["echoApprovers"]!).Count);
            Assert.AreEqual("D001", ((DepartmentRef)result["echoDept"]!).Value);
            Assert.AreEqual(2, ((List<DepartmentRef>)result["echoDepartments"]!).Count);
            Assert.AreEqual("/files/receipt.pdf", result["echoAttachment"]);
            CollectionAssert.AreEqual(new[] { "/files/a.pdf", "/files/b.pdf" }, ((List<string>)result["echoAttachments"]!).ToArray());
            Assert.AreEqual(2, ((List<SimulationLineItem>)result["echoItems"]!).Count);

            var dataContext = InvokeSavePluginNodeResult(execResult.Result, pluginSetting);
            var outputData = (IDictionary<string, object?>)dataContext.NodeDatas["plugin"].ActionDatas.Single().FormData.Data;
            Assert.IsTrue(outputData.ContainsKey("result"));
            Assert.AreEqual("paid", outputData["echoStatus"]);
            Assert.AreEqual("u1", ((IDictionary<string, object?>)outputData["echoOwner"]!)["id"]);
            Assert.AreEqual("/files/receipt.pdf", outputData["echoAttachment"]);

            var restoredNodeData = new EfNodeData
            {
                NodeId = "plugin",
                SingleResult = true,
                FormDataStr = dataContext.NodeDatas["plugin"].FormDataStr,
            };
            var restoredData = (IDictionary<string, object?>)restoredNodeData.ActionDatas.Single().FormData.Data;
            Assert.IsTrue(restoredData.ContainsKey("echoOwner"));
            Assert.IsTrue(restoredData.ContainsKey("echoAttachments"));
        }

        [TestMethod]
        public void PluginNode_ConfigToExecution_UsesConfiguredJsonOptionsForNumericStrings()
        {
            TestJsonOptions.UseProjectDefaults();
            var pluginSetting = ParsePluginSetting(BuildWorkflowContent(useNumericStrings: true));

            var payload = InvokeBuildPayload(pluginSetting, CreateSimulationScriptEngine());
            using var plugin = new SimulationPlugin();
            var execResult = plugin.Execute(
                pluginSetting,
                new PluginExecArgs
                {
                    FunName = "Echo",
                    FunArgs = payload.SerializeToJson()
                });

            Assert.AreEqual(0, execResult.Code, execResult.Message);
            var result = (IDictionary<string, object?>)execResult.Result!;
            Assert.AreEqual(12.34m, result["echoAmount"]);
            Assert.AreEqual(1710000000000L, result["echoBizDate"]);
        }

        [TestMethod]
        public void PluginNode_FieldMapping_ResolvesMainAndSubFieldsIntoPayload()
        {
            var pluginSetting = new PluginSetting
            {
                PluginId = "simulation-plugin",
                FunctionId = "Echo",
                FieldSettings =
                [
                    new PluginFieldSetting
                    {
                        FieldKey = "owner",
                        FieldType = PluginFieldKind.SingleEmployee,
                        ValueType = PluginValueType.Field,
                        ValueField = new PluginFieldReference
                        {
                            NodeId = "start",
                            FormId = "source-form",
                            Field = "owner",
                            FieldType = PluginFieldKind.SingleEmployee,
                            IsSubField = false,
                            SingleResultNode = true,
                        }
                    },
                    new PluginFieldSetting
                    {
                        FieldKey = "items",
                        FieldType = PluginFieldKind.TableForm,
                        ValueType = PluginValueType.Empty,
                        SubFieldSettings =
                        [
                            new PluginFieldSetting
                            {
                                FieldKey = "itemName",
                                FieldType = PluginFieldKind.Text,
                                ValueType = PluginValueType.Field,
                                ValueField = new PluginFieldReference
                                {
                                    NodeId = "start",
                                    FormId = "source-form",
                                    Field = "items>itemName",
                                    FieldType = PluginFieldKind.Text,
                                    IsSubField = true,
                                    SingleResultNode = true,
                                }
                            },
                            new PluginFieldSetting
                            {
                                FieldKey = "qty",
                                FieldType = PluginFieldKind.Number,
                                ValueType = PluginValueType.Field,
                                ValueField = new PluginFieldReference
                                {
                                    NodeId = "start",
                                    FormId = "source-form",
                                    Field = "items>qty",
                                    FieldType = PluginFieldKind.Number,
                                    IsSubField = true,
                                    SingleResultNode = true,
                                }
                            }
                        ]
                    }
                ]
            };

            var payload = InvokeBuildPayload(
                pluginSetting,
                new FakeScriptEngine(new Dictionary<string, object?>
                {
                    ["data.n_start.owner"] = OrgRef("u1", "E001", "Alice", 2),
                    ["MAP(data.n_start.items,'itemName')"] = new[] { "A", "B" },
                    ["MAP(data.n_start.items,'qty')"] = new object[] { 1m, 2m },
                }));

            var owner = (Dictionary<string, object?>)payload["owner"]!;
            Assert.AreEqual("u1", owner["id"]);
            var items = (List<Dictionary<string, object?>>)payload["items"]!;
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("A", items[0]["itemName"]);
            Assert.AreEqual(2m, items[1]["qty"]);
        }

        [TestMethod]
        public void PluginNode_SubListMapping_BroadcastsMainFieldsAcrossSourceRows()
        {
            var pluginSetting = new PluginSetting
            {
                PluginId = "simulation-plugin",
                FunctionId = "Echo",
                FieldSettings =
                [
                    new PluginFieldSetting
                    {
                        FieldKey = "items",
                        FieldType = PluginFieldKind.TableForm,
                        ValueType = PluginValueType.Empty,
                        SubFieldSettings =
                        [
                            new PluginFieldSetting
                            {
                                FieldKey = "itemName",
                                FieldType = PluginFieldKind.Text,
                                ValueType = PluginValueType.Field,
                                ValueField = new PluginFieldReference
                                {
                                    NodeId = "start",
                                    FormId = "source-form",
                                    Field = "items>itemName",
                                    FieldType = PluginFieldKind.Text,
                                    IsSubField = true,
                                    SingleResultNode = true,
                                }
                            },
                            new PluginFieldSetting
                            {
                                FieldKey = "price",
                                FieldType = PluginFieldKind.Number,
                                ValueType = PluginValueType.Field,
                                ValueField = new PluginFieldReference
                                {
                                    NodeId = "start",
                                    FormId = "source-form",
                                    Field = "defaultPrice",
                                    FieldType = PluginFieldKind.Number,
                                    IsSubField = false,
                                    SingleResultNode = true,
                                }
                            }
                        ]
                    }
                ]
            };

            var payload = InvokeBuildPayload(
                pluginSetting,
                new FakeScriptEngine(new Dictionary<string, object?>
                {
                    ["MAP(data.n_start.items,'itemName')"] = new[] { "A", "B" },
                    ["data.n_start.defaultPrice"] = 9m,
                }));

            var items = (List<Dictionary<string, object?>>)payload["items"]!;
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(9m, items[0]["price"]);
            Assert.AreEqual(9m, items[1]["price"]);
        }

        [TestMethod]
        public void PluginNode_SubListMapping_ExpandsMultiResultMainFieldsIntoRows()
        {
            var pluginSetting = new PluginSetting
            {
                PluginId = "simulation-plugin",
                FunctionId = "Echo",
                FieldSettings =
                [
                    new PluginFieldSetting
                    {
                        FieldKey = "items",
                        FieldType = PluginFieldKind.TableForm,
                        ValueType = PluginValueType.Empty,
                        SubFieldSettings =
                        [
                            new PluginFieldSetting
                            {
                                FieldKey = "itemName",
                                FieldType = PluginFieldKind.Text,
                                ValueType = PluginValueType.Field,
                                ValueField = new PluginFieldReference
                                {
                                    NodeId = "queryMany",
                                    FormId = "source-form",
                                    Field = "itemName",
                                    FieldType = PluginFieldKind.Text,
                                    IsSubField = false,
                                    SingleResultNode = false,
                                }
                            },
                            new PluginFieldSetting
                            {
                                FieldKey = "qty",
                                FieldType = PluginFieldKind.Number,
                                ValueType = PluginValueType.Field,
                                ValueField = new PluginFieldReference
                                {
                                    NodeId = "queryMany",
                                    FormId = "source-form",
                                    Field = "qty",
                                    FieldType = PluginFieldKind.Number,
                                    IsSubField = false,
                                    SingleResultNode = false,
                                }
                            }
                        ]
                    }
                ]
            };

            var payload = InvokeBuildPayload(
                pluginSetting,
                new FakeScriptEngine(new Dictionary<string, object?>
                {
                    ["MAP(data.n_queryMany,'itemName')"] = "[\"A\",\"B\"]",
                    ["MAP(data.n_queryMany,'qty')"] = new object[] { 1m, 2m },
                }));

            var items = (List<Dictionary<string, object?>>)payload["items"]!;
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("A", items[0]["itemName"]);
            Assert.AreEqual("B", items[1]["itemName"]);
            Assert.AreEqual(1m, items[0]["qty"]);
            Assert.AreEqual(2m, items[1]["qty"]);
        }

        [TestMethod]
        public void PluginNode_MainFieldMapping_KeepsMultiResultMainFieldExpressionScalar()
        {
            var pluginSetting = new PluginSetting
            {
                PluginId = "simulation-plugin",
                FunctionId = "Echo",
                FieldSettings =
                [
                    new PluginFieldSetting
                    {
                        FieldKey = "bizNo",
                        FieldType = PluginFieldKind.Text,
                        ValueType = PluginValueType.Field,
                        ValueField = new PluginFieldReference
                        {
                            NodeId = "queryMany",
                            FormId = "source-form",
                            Field = "bizNo",
                            FieldType = PluginFieldKind.Text,
                            IsSubField = false,
                            SingleResultNode = false,
                        }
                    }
                ]
            };

            var payload = InvokeBuildPayload(
                pluginSetting,
                new FakeScriptEngine(new Dictionary<string, object?>
                {
                    ["data.n_queryMany.bizNo"] = "BIZ-MAIN",
                }));

            Assert.AreEqual("BIZ-MAIN", payload["bizNo"]);
        }

        [TestMethod]
        public void SamplePlugin_ConfigToExecution_BindsSubListComplexFields()
        {
            TestJsonOptions.UseProjectDefaults();
            var pluginSetting = ParsePluginSetting(BuildSamplePluginWorkflowContent());

            using var plugin = new SampleReceiptPlugin();
            var receiptFunction = plugin.Description.Functions.Single(x => x.Id == "EchoReceipt");
            var itemField = receiptFunction.InputFields.Single(x => x.Key == "items");
            Assert.AreEqual(PluginFieldKind.TableForm, itemField.FieldType);
            Assert.AreEqual(8, itemField.SubFields.Count);
            Assert.AreEqual(PluginFieldKind.SingleEmployee, itemField.SubFields.Single(x => x.Key == "costOwner").FieldType);
            Assert.AreEqual(PluginFieldKind.SingleDepartment, itemField.SubFields.Single(x => x.Key == "costDept").FieldType);
            Assert.AreEqual(PluginFieldKind.FileUpload, itemField.SubFields.Single(x => x.Key == "evidenceFiles").FieldType);

            var payload = InvokeBuildPayload(pluginSetting, CreateSamplePluginScriptEngine());
            var payloadItems = (List<Dictionary<string, object?>>)payload["items"]!;
            Assert.AreEqual(2, payloadItems.Count);
            Assert.AreEqual("sample shared remark", payloadItems[0]["remark"]);
            Assert.AreEqual("travel", payloadItems[0]["category"]);
            Assert.AreEqual("u2", ((Dictionary<string, object?>)payloadItems[1]["costOwner"]!)["id"]);
            CollectionAssert.AreEqual(
                new[] { "/files/b.pdf", "/files/c.pdf" },
                ((IEnumerable<object?>)payloadItems[1]["evidenceFiles"]!).Select(x => x?.ToString()).ToArray());

            var execResult = plugin.Execute(
                pluginSetting,
                new PluginExecArgs
                {
                    FunName = "EchoReceipt",
                    FunArgs = payload.SerializeToJson()
                });

            Assert.AreEqual(0, execResult.Code, execResult.Message);
            var result = (IDictionary<string, object?>)execResult.Result!;
            Assert.AreEqual("SAMPLE-001", result["echoBizNo"]);
            var echoItems = (List<SampleReceiptItemArgs>)result["echoItems"]!;
            Assert.AreEqual(2, echoItems.Count);
            Assert.AreEqual("travel", echoItems[0].Category);
            Assert.AreEqual("u2", echoItems[1].CostOwner!.Id);
            Assert.AreEqual("D002", echoItems[1].CostDept!.Value);
            CollectionAssert.AreEqual(new[] { "/files/b.pdf", "/files/c.pdf" }, echoItems[1].EvidenceFiles);
            Assert.AreEqual("sample shared remark", echoItems[1].Remark);

            var dataContext = InvokeSavePluginNodeResult(execResult.Result, pluginSetting);
            var outputData = (IDictionary<string, object?>)dataContext.NodeDatas["plugin"].ActionDatas.Single().FormData.Data;
            var savedItems = (IEnumerable<object?>)outputData["echoItems"]!;
            var savedSecondItem = (IDictionary<string, object?>)savedItems.ElementAt(1)!;
            Assert.AreEqual("office", savedSecondItem["category"]);
            Assert.AreEqual("u2", ((IDictionary<string, object?>)savedSecondItem["costOwner"]!)["id"]);
        }

        private static PluginSetting ParsePluginSetting(string content)
        {
            var parser = new WfMetadataParser();
            var definition = new Wf_Definition
            {
                CorpId = "corp-plugin",
                ExternalId = "eventFlow-plugin-simulation",
                Version = 1,
                FlowType = FlowType.EventFlow,
                Content = content,
            };

            var (metadata, _) = parser.Parse(definition);
            return metadata.Steps.Single(x => x.Id == "plugin").EfNodeSetting!.PluginSetting!;
        }

        private static string BuildWorkflowContent(bool useNumericStrings)
        {
            object amount = useNumericStrings ? "12.34" : 12.34m;
            object bizDate = useNumericStrings ? "1710000000000" : 1710000000000L;

            return new
            {
                StartNode = new
                {
                    Id = "start",
                    Name = "start",
                    NodeType = WfNodeType.Start,
                    NextId = "plugin",
                    Metadata = new
                    {
                        TriggerMeta = new
                        {
                            EventType = EventType.Submitted,
                            FormId = "source-form",
                            WfNodeId = "submit-node",
                            NodeAction = "submit",
                            SingleResult = true,
                        }
                    }
                },
                Nodes = new[]
                {
                    new
                    {
                        Id = "plugin",
                        Name = "plugin",
                        NodeType = WfNodeType.Plugin,
                        NextId = "end",
                        Metadata = new
                        {
                            PluginMeta = new
                            {
                                SingleResult = true,
                                PluginId = "simulation-plugin",
                                FunctionId = "Echo",
                                FieldSettings = new object[]
                                {
                                    Custom("bizNo", PluginFieldKind.Text, "BIZ-001"),
                                    Custom("amount", PluginFieldKind.Number, amount),
                                    Custom("bizDate", PluginFieldKind.Timestamp, bizDate),
                                    Custom("remark", PluginFieldKind.TextArea, "memo"),
                                    Custom("status", PluginFieldKind.SingleSelect, "paid"),
                                    Custom("tags", PluginFieldKind.MultipleSelect, new[] { "urgent", "finance" }),
                                    Custom("owner", PluginFieldKind.SingleEmployee, OrgRef("u1", "E001", "Alice", 2)),
                                    Custom("approvers", PluginFieldKind.MultipleEmployee, new[]
                                    {
                                        OrgRef("u1", "E001", "Alice", 2),
                                        OrgRef("u2", "E002", "Bob", 2),
                                    }),
                                    Custom("dept", PluginFieldKind.SingleDepartment, OrgRef("d1", "D001", "Finance", 1)),
                                    Custom("departments", PluginFieldKind.MultipleDepartment, new[]
                                    {
                                        OrgRef("d1", "D001", "Finance", 1),
                                        OrgRef("d2", "D002", "Ops", 1),
                                    }),
                                    Custom("attachment", PluginFieldKind.FileUpload, "/files/receipt.pdf"),
                                    Custom("attachments", PluginFieldKind.FileUpload, new[] { "/files/a.pdf", "/files/b.pdf" }),
                                    SubList("items",
                                        Field("itemName", PluginFieldKind.Text, "items>itemName", PluginFieldKind.Text),
                                        Field("qty", PluginFieldKind.Number, "items>qty", PluginFieldKind.Number),
                                        Field("price", PluginFieldKind.Number, "items>price", PluginFieldKind.Number)),
                                },
                                ResultFields = new object[]
                                {
                                    Result("echoBizNo", PluginFieldKind.Text),
                                    Result("echoAmount", PluginFieldKind.Number),
                                    Result("echoBizDate", PluginFieldKind.Timestamp),
                                    Result("echoStatus", PluginFieldKind.SingleSelect),
                                    Result("echoTags", PluginFieldKind.MultipleSelect),
                                    Result("echoOwner", PluginFieldKind.SingleEmployee),
                                    Result("echoApprovers", PluginFieldKind.MultipleEmployee),
                                    Result("echoDept", PluginFieldKind.SingleDepartment),
                                    Result("echoDepartments", PluginFieldKind.MultipleDepartment),
                                    Result("echoAttachment", PluginFieldKind.FileUpload),
                                    Result("echoAttachments", PluginFieldKind.FileUpload),
                                    Result("echoItems", PluginFieldKind.TableForm),
                                }
                            }
                        }
                    }
                },
                EndNode = new
                {
                    Id = "end",
                    Name = "end",
                    NodeType = WfNodeType.End,
                    Metadata = new { }
                }
            }.SerializeToJson();
        }

        private static string BuildSamplePluginWorkflowContent()
        {
            return new
            {
                StartNode = new
                {
                    Id = "start",
                    Name = "start",
                    NodeType = WfNodeType.Start,
                    NextId = "plugin",
                    Metadata = new
                    {
                        TriggerMeta = new
                        {
                            EventType = EventType.Submitted,
                            FormId = "source-form",
                            WfNodeId = "submit-node",
                            NodeAction = "submit",
                            SingleResult = true,
                        }
                    }
                },
                Nodes = new[]
                {
                    new
                    {
                        Id = "plugin",
                        Name = "plugin",
                        NodeType = WfNodeType.Plugin,
                        NextId = "end",
                        Metadata = new
                        {
                            PluginMeta = new
                            {
                                SingleResult = true,
                                PluginId = "sampleplugin",
                                FunctionId = "EchoReceipt",
                                FieldSettings = new object[]
                                {
                                    Custom("bizNo", PluginFieldKind.Text, "SAMPLE-001"),
                                    Custom("amount", PluginFieldKind.Number, 88.6m),
                                    Custom("bizDate", PluginFieldKind.Timestamp, 1710000000000L),
                                    Custom("remark", PluginFieldKind.TextArea, "sample receipt"),
                                    Custom("status", PluginFieldKind.SingleSelect, "paid"),
                                    Custom("receiver", PluginFieldKind.SingleEmployee, OrgRef("u1", "E001", "Alice", 2)),
                                    Custom("dept", PluginFieldKind.SingleDepartment, OrgRef("d1", "D001", "Finance", 1)),
                                    Custom("attachments", PluginFieldKind.FileUpload, new[] { "/files/receipt.pdf" }),
                                    Custom("images", PluginFieldKind.ImageUpload, new[] { "/files/receipt.png" }),
                                    SubList("items",
                                        Field("itemName", PluginFieldKind.Text, "details>itemName", PluginFieldKind.Text),
                                        Field("qty", PluginFieldKind.Number, "details>qty", PluginFieldKind.Number),
                                        Field("price", PluginFieldKind.Number, "details>price", PluginFieldKind.Number),
                                        Field("category", PluginFieldKind.SingleSelect, "details>category", PluginFieldKind.SingleSelect),
                                        Field("costOwner", PluginFieldKind.SingleEmployee, "details>costOwner", PluginFieldKind.SingleEmployee),
                                        Field("costDept", PluginFieldKind.SingleDepartment, "details>costDept", PluginFieldKind.SingleDepartment),
                                        Field("evidenceFiles", PluginFieldKind.FileUpload, "details>evidenceFiles", PluginFieldKind.FileUpload),
                                        Field("remark", PluginFieldKind.TextArea, "lineRemark", PluginFieldKind.TextArea)),
                                },
                                ResultFields = new object[]
                                {
                                    Result("echoBizNo", PluginFieldKind.Text),
                                    Result("echoAmount", PluginFieldKind.Number),
                                    Result("echoStatus", PluginFieldKind.SingleSelect),
                                    Result("echoReceiver", PluginFieldKind.SingleEmployee),
                                    Result("echoDept", PluginFieldKind.SingleDepartment),
                                    Result(
                                        "echoItems",
                                        PluginFieldKind.TableForm,
                                        Result("itemName", PluginFieldKind.Text),
                                        Result("qty", PluginFieldKind.Number),
                                        Result("price", PluginFieldKind.Number),
                                        Result("category", PluginFieldKind.SingleSelect),
                                        Result("costOwner", PluginFieldKind.SingleEmployee),
                                        Result("costDept", PluginFieldKind.SingleDepartment),
                                        Result("evidenceFiles", PluginFieldKind.FileUpload),
                                        Result("remark", PluginFieldKind.TextArea)),
                                }
                            }
                        }
                    }
                },
                EndNode = new
                {
                    Id = "end",
                    Name = "end",
                    NodeType = WfNodeType.End,
                    Metadata = new { }
                }
            }.SerializeToJson();
        }

        private static object Custom(string key, string fieldType, object? value)
        {
            return new
            {
                FieldKey = key,
                FieldType = fieldType,
                Value = new
                {
                    Type = PluginValueType.Custom.ToString(),
                    Value = value,
                }
            };
        }

        private static object SubList(string key, params object[] subFieldSettings)
        {
            return new
            {
                FieldKey = key,
                FieldType = PluginFieldKind.TableForm,
                Value = new
                {
                    Type = PluginValueType.Empty.ToString(),
                },
                SubFieldSettings = subFieldSettings,
            };
        }

        private static object Field(string key, string fieldType, string sourceField, string sourceFieldType)
        {
            return new
            {
                FieldKey = key,
                FieldType = fieldType,
                Value = new
                {
                    Type = PluginValueType.Field.ToString(),
                    FieldValue = new
                    {
                        NodeId = "start",
                        FormId = "source-form",
                        Field = sourceField,
                        Type = sourceFieldType,
                        IsSubField = sourceField.Contains('>'),
                        SingleResultNode = true,
                    }
                }
            };
        }

        private static FakeScriptEngine CreateSimulationScriptEngine()
        {
            return new FakeScriptEngine(new Dictionary<string, object?>
            {
                ["MAP(data.n_start.items,'itemName')"] = new[] { "A", "B" },
                ["MAP(data.n_start.items,'qty')"] = new object[] { 1m, 2m },
                ["MAP(data.n_start.items,'price')"] = new object[] { 10m, 20m },
            });
        }

        private static FakeScriptEngine CreateSamplePluginScriptEngine()
        {
            return new FakeScriptEngine(new Dictionary<string, object?>
            {
                ["MAP(data.n_start.details,'itemName')"] = new[] { "交通费", "办公用品" },
                ["MAP(data.n_start.details,'qty')"] = new object[] { 1m, 2m },
                ["MAP(data.n_start.details,'price')"] = new object[] { 30m, 29.3m },
                ["MAP(data.n_start.details,'category')"] = new[] { "travel", "office" },
                ["MAP(data.n_start.details,'costOwner')"] = new object[]
                {
                    OrgRef("u1", "E001", "Alice", 2),
                    OrgRef("u2", "E002", "Bob", 2),
                },
                ["MAP(data.n_start.details,'costDept')"] = new object[]
                {
                    OrgRef("d1", "D001", "Finance", 1),
                    OrgRef("d2", "D002", "Ops", 1),
                },
                ["MAP(data.n_start.details,'evidenceFiles')"] = new object[]
                {
                    new[] { "/files/a.pdf" },
                    new[] { "/files/b.pdf", "/files/c.pdf" },
                },
                ["data.n_start.lineRemark"] = "sample shared remark",
            });
        }

        private static Dictionary<string, object?> OrgRef(string id, string value, string label, int type)
        {
            return new Dictionary<string, object?>
            {
                ["id"] = id,
                ["value"] = value,
                ["label"] = label,
                ["type"] = type,
            };
        }

        private static object Result(string key, string fieldType, params object[] subFields)
        {
            return new
            {
                FieldKey = key,
                FieldName = key,
                FieldType = fieldType,
                SubFields = subFields,
            };
        }

        private static Dictionary<string, object?> InvokeBuildPayload(PluginSetting pluginSetting, IScriptEngine? scriptEngine = null)
        {
            var node = CreateUninitializedPluginNode();
            if (scriptEngine != null)
            {
                typeof(EfNodeBase<EfPluginNode>)
                    .GetField("<ScriptEngine>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(node, scriptEngine);
            }

            var method = typeof(EfPluginNode).GetMethod("BuildPayload", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (Dictionary<string, object?>)method.Invoke(node, new object[] { new EfDataContext(), pluginSetting })!;
        }

        private static EfDataContext InvokeSavePluginNodeResult(object? pluginResult, PluginSetting pluginSetting)
        {
            var node = CreateUninitializedPluginNode();
            node.Metadata = new WfStep { Id = "plugin", Name = "plugin", NodeType = WfNodeType.Plugin };
            var dataContext = new EfDataContext
            {
                AppId = "app",
                CorpId = "corp-plugin",
            };
            var method = typeof(EfPluginNode).GetMethod("SavePluginNodeResult", BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(node, new[] { dataContext, pluginResult, pluginSetting });
            return dataContext;
        }

        private static EfPluginNode CreateUninitializedPluginNode()
        {
#pragma warning disable SYSLIB0050
            return (EfPluginNode)FormatterServices.GetUninitializedObject(typeof(EfPluginNode));
#pragma warning restore SYSLIB0050
        }

        [Plugin("simulation-plugin", "Simulation Plugin", Version = "1.0")]
        private sealed class SimulationPlugin : PluginBase<SimulationSetting>
        {
            [PluginFunction("Echo", "Echo")]
            private SimulationResult Echo(SimulationArgs args)
            {
                return new SimulationResult
                {
                    EchoBizNo = args.BizNo,
                    EchoAmount = args.Amount,
                    EchoBizDate = args.BizDate,
                    EchoStatus = args.Status,
                    EchoTags = args.Tags,
                    EchoOwner = args.Owner,
                    EchoApprovers = args.Approvers,
                    EchoDept = args.Dept,
                    EchoDepartments = args.Departments,
                    EchoAttachment = args.Attachment,
                    EchoAttachments = args.Attachments,
                    EchoItems = args.Items,
                };
            }
        }

        private sealed class SimulationSetting
        {
        }

        private sealed class SimulationArgs : PluginSubList<SimulationLineItem>
        {
            [PluginInput("BizNo", PluginFieldKind.Text, Key = "bizNo")]
            public string? BizNo { get; set; }

            [PluginInput("Amount", PluginFieldKind.Number, Key = "amount")]
            public decimal Amount { get; set; }

            [PluginInput("BizDate", PluginFieldKind.Timestamp, Key = "bizDate")]
            public long? BizDate { get; set; }

            [PluginInput("Remark", PluginFieldKind.TextArea, Key = "remark")]
            public string? Remark { get; set; }

            [PluginInput("Status", PluginFieldKind.SingleSelect, Key = "status")]
            public string? Status { get; set; }

            [PluginInput("Tags", PluginFieldKind.MultipleSelect, Key = "tags")]
            public List<string> Tags { get; set; } = [];

            [PluginInput("Owner", PluginFieldKind.SingleEmployee, Key = "owner")]
            public EmployeeRef? Owner { get; set; }

            [PluginInput("Approvers", PluginFieldKind.MultipleEmployee, Key = "approvers")]
            public List<EmployeeRef> Approvers { get; set; } = [];

            [PluginInput("Dept", PluginFieldKind.SingleDepartment, Key = "dept")]
            public DepartmentRef? Dept { get; set; }

            [PluginInput("Departments", PluginFieldKind.MultipleDepartment, Key = "departments")]
            public List<DepartmentRef> Departments { get; set; } = [];

            [PluginInput("Attachment", PluginFieldKind.FileUpload, Key = "attachment")]
            public string? Attachment { get; set; }

            [PluginInput("Attachments", PluginFieldKind.FileUpload, Key = "attachments")]
            public List<string> Attachments { get; set; } = [];

            [PluginSubList("Items", Key = "items")]
            public List<SimulationLineItem> Items { get; set; } = [];
        }

        private sealed class SimulationResult : PluginSubList<SimulationLineItem>
        {
            [PluginOutput("EchoBizNo", PluginFieldKind.Text, Key = "echoBizNo")]
            public string? EchoBizNo { get; set; }

            [PluginOutput("EchoAmount", PluginFieldKind.Number, Key = "echoAmount")]
            public decimal EchoAmount { get; set; }

            [PluginOutput("EchoBizDate", PluginFieldKind.Timestamp, Key = "echoBizDate")]
            public long? EchoBizDate { get; set; }

            [PluginOutput("EchoStatus", PluginFieldKind.SingleSelect, Key = "echoStatus")]
            public string? EchoStatus { get; set; }

            [PluginOutput("EchoTags", PluginFieldKind.MultipleSelect, Key = "echoTags")]
            public List<string> EchoTags { get; set; } = [];

            [PluginOutput("EchoOwner", PluginFieldKind.SingleEmployee, Key = "echoOwner")]
            public EmployeeRef? EchoOwner { get; set; }

            [PluginOutput("EchoApprovers", PluginFieldKind.MultipleEmployee, Key = "echoApprovers")]
            public List<EmployeeRef> EchoApprovers { get; set; } = [];

            [PluginOutput("EchoDept", PluginFieldKind.SingleDepartment, Key = "echoDept")]
            public DepartmentRef? EchoDept { get; set; }

            [PluginOutput("EchoDepartments", PluginFieldKind.MultipleDepartment, Key = "echoDepartments")]
            public List<DepartmentRef> EchoDepartments { get; set; } = [];

            [PluginOutput("EchoAttachment", PluginFieldKind.FileUpload, Key = "echoAttachment")]
            public string? EchoAttachment { get; set; }

            [PluginOutput("EchoAttachments", PluginFieldKind.FileUpload, Key = "echoAttachments")]
            public List<string> EchoAttachments { get; set; } = [];

            [PluginSubList("EchoItems", Key = "echoItems")]
            public List<SimulationLineItem> EchoItems { get; set; } = [];
        }

        private sealed class SimulationLineItem : PluginField
        {
            [PluginInput("ItemName", PluginFieldKind.Text, Key = "itemName")]
            [PluginOutput("ItemName", PluginFieldKind.Text, Key = "itemName")]
            public string? ItemName { get; set; }

            [PluginInput("Qty", PluginFieldKind.Number, Key = "qty")]
            [PluginOutput("Qty", PluginFieldKind.Number, Key = "qty")]
            public decimal Qty { get; set; }

            [PluginInput("Price", PluginFieldKind.Number, Key = "price")]
            [PluginOutput("Price", PluginFieldKind.Number, Key = "price")]
            public decimal Price { get; set; }
        }

        private sealed class FakeScriptEngine(IReadOnlyDictionary<string, object?> values) : IScriptEngine
        {
            public EvaluationResult<dynamic> Evaluate(string script, IDictionary<string, object>? parameters = null, CancellationToken ct = default)
            {
                return new EvaluationResult<dynamic> { Value = values[script] };
            }

            public EvaluationResult<T> Evaluate<T>(string script, IDictionary<string, object>? parameters = null, CancellationToken ct = default)
            {
                return new EvaluationResult<T> { Value = (T?)values[script] };
            }

            public void Dispose()
            {
            }
        }
    }
}
