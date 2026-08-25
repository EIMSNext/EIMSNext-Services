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
                ExternalId = "eventFlow-001",
                Version = 1,
                FlowType = FlowType.EventFlow,
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
                                            SubFields = new object[]
                                            {
                                                new
                                                {
                                                    FieldKey = "payeeName",
                                                    FieldName = "收方户名",
                                                    FieldType = PluginFieldKind.Text,
                                                }
                                            }
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

            var pluginSetting = metadata.Steps.Single(x => x.Id == "plugin").EfNodeSetting!.PluginSetting!;
            Assert.AreEqual("sampleplugin", pluginSetting.PluginId);
            Assert.AreEqual("classifyReceipt", pluginSetting.FunctionId);
            Assert.AreEqual(2, pluginSetting.FieldSettings.Count);
            Assert.AreEqual(PluginFieldKind.SingleSelect, pluginSetting.FieldSettings[0].FieldType);
            Assert.AreEqual(PluginValueType.Custom, pluginSetting.FieldSettings[0].ValueType);
            Assert.AreEqual(PluginFieldKind.SingleEmployee, pluginSetting.FieldSettings[1].ValueField!.FieldType);
            Assert.AreEqual(2, pluginSetting.ResultFields.Count);
            Assert.AreEqual(PluginFieldKind.TableForm, pluginSetting.ResultFields[0].FieldType);
            Assert.AreEqual("回写明细", pluginSetting.ResultFields[0].FieldName);
            Assert.AreEqual(1, pluginSetting.ResultFields[0].SubFields.Count);
            Assert.AreEqual("payeeName", pluginSetting.ResultFields[0].SubFields[0].FieldKey);
            Assert.AreEqual(PluginFieldKind.SingleDepartment, pluginSetting.ResultFields[1].FieldType);
            Assert.AreEqual("ownerDept", pluginSetting.ResultFields[1].FieldName);
        }

        [TestMethod]
        public void Parse_PluginNode_RejectsSubListFieldsFromDifferentSourceTables()
        {
            var parser = new WfMetadataParser();
            var definition = new Wf_Definition
            {
                CorpId = "corp-plugin",
                ExternalId = "eventFlow-invalid-sublist",
                Version = 1,
                FlowType = FlowType.EventFlow,
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
                                            FieldKey = "paymentDetails",
                                            FieldType = PluginFieldKind.TableForm,
                                            Value = new
                                            {
                                                Type = PluginValueType.Empty.ToString(),
                                            },
                                            SubFieldSettings = new object[]
                                            {
                                                SubField("payeeName", "paymentDetails>payeeName"),
                                                SubField("accountNo", "otherDetails>accountNo"),
                                            }
                                        }
                                    },
                                    ResultFields = Array.Empty<object>()
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

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => parser.Parse(definition));
            StringAssert.Contains(exception.Message, "same iterable source");
        }

        [TestMethod]
        public void Parse_PluginNode_AllowsDifferentPluginSubListsToUseDifferentSourceTables()
        {
            var parser = new WfMetadataParser();
            var definition = CreatePluginDefinition(new object[]
            {
                TableField(
                    "paymentDetails",
                    SubField("payeeName", "paymentDetails>payeeName"),
                    SubField("accountNo", "paymentDetails>accountNo")),
                TableField(
                    "otherDetails",
                    SubField("payeeName", "otherDetails>payeeName"),
                    SubField("accountNo", "otherDetails>accountNo")),
            });

            var (metadata, _) = parser.Parse(definition);

            var pluginSetting = metadata.Steps.Single(x => x.Id == "plugin").EfNodeSetting!.PluginSetting!;
            Assert.AreEqual(2, pluginSetting.FieldSettings.Count);
            Assert.AreEqual("paymentDetails", pluginSetting.FieldSettings[0].FieldKey);
            Assert.AreEqual("otherDetails", pluginSetting.FieldSettings[1].FieldKey);
        }

        [TestMethod]
        public void Parse_PluginNode_AllowsSubListFieldsFromSameMultiResultMainSource()
        {
            var parser = new WfMetadataParser();
            var definition = CreatePluginDefinition(new object[]
            {
                TableField(
                    "paymentDetails",
                    SubField("payeeName", "title", singleResultNode: false),
                    SubField("accountNo", "amount", singleResultNode: false)),
            });

            var (metadata, _) = parser.Parse(definition);

            var pluginSetting = metadata.Steps.Single(x => x.Id == "plugin").EfNodeSetting!.PluginSetting!;
            var subFields = pluginSetting.FieldSettings.Single().SubFieldSettings;
            Assert.AreEqual(2, subFields.Count);
            Assert.AreEqual(false, subFields[0].ValueField!.SingleResultNode);
            Assert.AreEqual(false, subFields[1].ValueField!.SingleResultNode);
        }

        [TestMethod]
        public void Parse_PluginNode_RejectsSubListFieldsMixingMultiResultMainAndItsSubTable()
        {
            var parser = new WfMetadataParser();
            var definition = CreatePluginDefinition(new object[]
            {
                TableField(
                    "paymentDetails",
                    SubField("payeeName", "title", singleResultNode: false),
                    SubField("accountNo", "paymentDetails>accountNo", singleResultNode: false)),
            });

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => parser.Parse(definition));
            StringAssert.Contains(exception.Message, "same iterable source");
        }

        [TestMethod]
        public void Parse_InsertNode_RejectsSameTargetSubListFromDifferentIterableSources()
        {
            var parser = new WfMetadataParser();
            var definition = CreateFormMappingDefinition(
                WfNodeType.Insert,
                new object[]
                {
                    FormField("paymentDetails>payeeName", "sourceLines>payeeName"),
                    FormField("paymentDetails>accountNo", "otherLines>accountNo"),
                });

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => parser.Parse(definition));
            StringAssert.Contains(exception.Message, "Insert node [form-node]");
            StringAssert.Contains(exception.Message, "same iterable source");
        }

        [TestMethod]
        public void Parse_UpdateNode_RejectsSameTargetSubListFromDifferentIterableSources()
        {
            var parser = new WfMetadataParser();
            var definition = CreateFormMappingDefinition(
                WfNodeType.Update,
                new object[]
                {
                    FormField("paymentDetails>payeeName", "sourceLines>payeeName"),
                    FormField("paymentDetails>accountNo", "otherLines>accountNo"),
                });

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => parser.Parse(definition));
            StringAssert.Contains(exception.Message, "Update node [form-node]");
            StringAssert.Contains(exception.Message, "same iterable source");
        }

        [TestMethod]
        public void Parse_UpdateNode_RejectsMultiResultSubFieldToSubField()
        {
            var parser = new WfMetadataParser();
            var definition = CreateFormMappingDefinition(
                WfNodeType.Update,
                new object[]
                {
                    FormField("paymentDetails>payeeName", "sourceLines>payeeName", singleResultNode: false),
                });

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => parser.Parse(definition));
            StringAssert.Contains(exception.Message, "cannot map multi-result subfields");
        }

        [TestMethod]
        public void Parse_UpdateNode_InsertIfNoData_RejectsSameTargetSubListFromDifferentIterableSources()
        {
            var parser = new WfMetadataParser();
            var definition = CreateFormMappingDefinition(
                WfNodeType.Update,
                Array.Empty<object>(),
                new object[]
                {
                    FormField("paymentDetails>payeeName", "sourceLines>payeeName"),
                    FormField("paymentDetails>accountNo", "otherLines>accountNo"),
                });

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => parser.Parse(definition));
            StringAssert.Contains(exception.Message, "insert-if-no-data");
            StringAssert.Contains(exception.Message, "same iterable source");
        }

        [TestMethod]
        public void Parse_InsertNode_AllowsDifferentTargetSubListsFromDifferentIterableSources()
        {
            var parser = new WfMetadataParser();
            var definition = CreateFormMappingDefinition(
                WfNodeType.Insert,
                new object[]
                {
                    FormField("paymentDetails>payeeName", "sourceLines>payeeName"),
                    FormField("otherDetails>accountNo", "otherLines>accountNo"),
                });

            var (metadata, _) = parser.Parse(definition);

            var insertSetting = metadata.Steps.Single(x => x.Id == "form-node").EfNodeSetting!.InsertSetting!;
            Assert.AreEqual(2, insertSetting.FieldSettings.Count);
        }

        [TestMethod]
        public void Parse_PluginNode_RejectsMainPluginFieldMappedToSubTableField()
        {
            var parser = new WfMetadataParser();
            var definition = CreatePluginDefinition(new object[]
            {
                new
                {
                    FieldKey = "title",
                    FieldType = PluginFieldKind.Text,
                    Value = new
                    {
                        Type = PluginValueType.Field.ToString(),
                        FieldValue = new
                        {
                            NodeId = "start",
                            FormId = "source-form",
                            Field = "paymentDetails>payeeName",
                            Type = PluginFieldKind.Text,
                            IsSubField = true,
                            SingleResultNode = true,
                        }
                    }
                }
            });

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => parser.Parse(definition));
            StringAssert.Contains(exception.Message, "cannot map a sub table field");
        }

        private static Wf_Definition CreatePluginDefinition(object[] fieldSettings)
        {
            return new Wf_Definition
            {
                CorpId = "corp-plugin",
                ExternalId = "eventFlow-plugin-source-rules",
                Version = 1,
                FlowType = FlowType.EventFlow,
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
                                    FieldSettings = fieldSettings,
                                    ResultFields = Array.Empty<object>()
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
        }

        private static Wf_Definition CreateFormMappingDefinition(
            WfNodeType nodeType,
            object[] fieldSettings,
            object[]? insertIfNoDataSettings = null)
        {
            return new Wf_Definition
            {
                CorpId = "corp-plugin",
                ExternalId = $"eventFlow-{nodeType}-source-rules",
                Version = 1,
                FlowType = FlowType.EventFlow,
                Content = new
                {
                    StartNode = new
                    {
                        Id = "start",
                        Name = "start",
                        NodeType = WfNodeType.Start,
                        NextId = "form-node",
                        Metadata = new
                        {
                            TriggerMeta = new
                            {
                                EventType = EventType.Submitted,
                                FormId = "source-form",
                                SingleResult = true,
                            }
                        }
                    },
                    Nodes = new[]
                    {
                        new
                        {
                            Id = "form-node",
                            Name = "form-node",
                            NodeType = nodeType,
                            NextId = "end",
                            Metadata = new
                            {
                                InsertMeta = nodeType == WfNodeType.Insert
                                    ? new
                                    {
                                        FormId = "target-form",
                                        SingleResult = true,
                                        FormFieldList = new
                                        {
                                            Items = fieldSettings,
                                        },
                                    }
                                    : null,
                                UpdateMeta = nodeType == WfNodeType.Update
                                    ? new
                                    {
                                        UpdateMode = UpdateMode.Node,
                                        NodeId = "start",
                                        FormId = "target-form",
                                        SingleResult = true,
                                        InsertIfNoData = insertIfNoDataSettings != null,
                                        FormFieldList = new
                                        {
                                            Items = fieldSettings,
                                        },
                                        InsertFieldList = new
                                        {
                                            Items = insertIfNoDataSettings ?? Array.Empty<object>(),
                                        },
                                    }
                                    : null,
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
        }

        private static object TableField(string key, params object[] subFields)
        {
            return new
            {
                FieldKey = key,
                FieldType = PluginFieldKind.TableForm,
                Value = new
                {
                    Type = PluginValueType.Empty.ToString(),
                },
                SubFieldSettings = subFields,
            };
        }

        private static object SubField(string key, string sourceField, bool singleResultNode = true)
        {
            return new
            {
                FieldKey = key,
                FieldType = PluginFieldKind.Text,
                Value = new
                {
                    Type = PluginValueType.Field.ToString(),
                    FieldValue = new
                    {
                        NodeId = "start",
                        FormId = "source-form",
                        Field = sourceField,
                        Type = PluginFieldKind.Text,
                        IsSubField = sourceField.Contains('>'),
                        SingleResultNode = singleResultNode,
                    }
                }
            };
        }

        private static object FormField(string targetField, string sourceField, bool singleResultNode = true)
        {
            return new
            {
                Field = new
                {
                    FormId = "target-form",
                    Field = targetField,
                    Type = PluginFieldKind.Text,
                    IsSubField = targetField.Contains('>'),
                },
                Value = new
                {
                    Type = FieldValueType.Field.ToString(),
                    FieldValue = new
                    {
                        NodeId = "start",
                        FormId = "source-form",
                        Field = sourceField,
                        Type = PluginFieldKind.Text,
                        IsSubField = sourceField.Contains('>'),
                        SingleResultNode = singleResultNode,
                    }
                }
            };
        }
    }
}
