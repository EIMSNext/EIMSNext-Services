using System.Text.Json;

using EIMSNext.Component;
using EIMSNext.Plugin.Contracts;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class WfMetadataParserPluginTests
    {
        [TestMethod]
        public void Parse_PluginNode_KeepsFieldAndResultTypesFromWorkflowJson()
        {
            var parser = new WfMetadataParser();
            var definition = new Wf_Definition
            {
                CorpId = "corp-plugin",
                ExternalId = "dataflow-001",
                Version = 1,
                FlowType = FlowType.Dataflow,
                Content = new
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
                                    FunctionId = "classifyReceipt",
                                    FieldSettings = new object[]
                                    {
                                        new
                                        {
                                            FieldKey = "category",
                                            FieldType = PluginFieldKind.SingleSelect,
                                            Value = new
                                            {
                                                Type = PluginValueType.Custom.ToString(),
                                                Value = new
                                                {
                                                    Value = "travel",
                                                    Label = "差旅"
                                                }
                                            }
                                        },
                                        new
                                        {
                                            FieldKey = "owner",
                                            FieldType = PluginFieldKind.SingleEmployee,
                                            Value = new
                                            {
                                                Type = PluginValueType.Field.ToString(),
                                                FieldValue = new
                                                {
                                                    NodeId = "start",
                                                    FormId = "source-form",
                                                    Field = "owner",
                                                    Type = PluginFieldKind.SingleEmployee,
                                                    IsSubField = false,
                                                    SingleResultNode = true,
                                                }
                                            }
                                        }
                                    },
                                    ResultFields = new object[]
                                    {
                                        new
                                        {
                                            FieldKey = "echoItems",
                                            FieldName = "回写明细",
                                            FieldType = PluginFieldKind.TableForm,
                                        },
                                        new
                                        {
                                            FieldKey = "ownerDept",
                                            FieldName = "",
                                            FieldType = PluginFieldKind.SingleDepartment,
                                        }
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
                }.SerializeToJson()
            };

            var (metadata, _) = parser.Parse(definition);

            var pluginSetting = metadata.Steps.Single(x => x.Id == "plugin").DfNodeSetting!.PluginSetting!;
            Assert.AreEqual("sampleplugin", pluginSetting.PluginId);
            Assert.AreEqual("classifyReceipt", pluginSetting.FunctionId);
            Assert.AreEqual(2, pluginSetting.FieldSettings.Count);
            Assert.AreEqual(PluginFieldKind.SingleSelect, pluginSetting.FieldSettings[0].FieldType);
            Assert.AreEqual(PluginValueType.Custom, pluginSetting.FieldSettings[0].ValueType);
            Assert.AreEqual(PluginFieldKind.SingleEmployee, pluginSetting.FieldSettings[1].ValueField!.FieldType);
            Assert.AreEqual(2, pluginSetting.ResultFields.Count);
            Assert.AreEqual(PluginFieldKind.TableForm, pluginSetting.ResultFields[0].FieldType);
            Assert.AreEqual("回写明细", pluginSetting.ResultFields[0].FieldName);
            Assert.AreEqual(PluginFieldKind.SingleDepartment, pluginSetting.ResultFields[1].FieldType);
            Assert.AreEqual("ownerDept", pluginSetting.ResultFields[1].FieldName);
        }
    }
}
