using System.Text.Json.Nodes;

using EIMSNext.Plugin.Contracts;

namespace EIMSNext.Flow.Tests
{
    [TestClass]
    public class PluginAttributeContractTests
    {
        [TestMethod]
        public void Description_Builds_From_Attributes()
        {
            using var plugin = new AttributePlugin();

            var desc = plugin.Description;

            Assert.AreEqual("attribute-plugin", desc.Id);
            Assert.AreEqual("Attribute Plugin", desc.Name);
            Assert.AreEqual("1.2", desc.Version);
            Assert.AreEqual(1, desc.Functions.Count);
            Assert.AreEqual("Echo", desc.Functions[0].Id);
            Assert.AreEqual(5, desc.Functions[0].InputFields.Count);
            Assert.AreEqual(PluginFieldKind.SingleSelect, desc.Functions[0].InputFields[0].FieldType);
            Assert.AreEqual(PluginFieldKind.SingleEmployee, desc.Functions[0].InputFields[1].FieldType);
            Assert.AreEqual(PluginFieldKind.SingleDepartment, desc.Functions[0].InputFields[2].FieldType);
            Assert.AreEqual(PluginFieldKind.MultipleSelect, desc.Functions[0].InputFields[3].FieldType);
            Assert.AreEqual(PluginFieldKind.FileUpload, desc.Functions[0].InputFields[4].FieldType);
            Assert.IsFalse(desc.Functions[0].InputFields[0].Multiple);
            Assert.IsTrue(desc.Functions[0].InputFields[3].Multiple);
            Assert.AreEqual(6, desc.Functions[0].ResultFields.Count);
        }

        [TestMethod]
        public void Description_Builds_SubList_Fields()
        {
            using var plugin = new SubListPlugin();

            var desc = plugin.Description;
            var function = desc.Functions.Single();

            Assert.AreEqual(2, function.InputFields.Count);
            var detail = function.InputFields.Single(x => x.Key == "paymentDetails");
            Assert.AreEqual(PluginFieldKind.TableForm, detail.FieldType);
            Assert.IsTrue(detail.Multiple);
            Assert.IsFalse(detail.AllowCustomValue);
            Assert.AreEqual(3, detail.SubFields.Count);
            Assert.AreEqual("payeeName", detail.SubFields[0].Key);
            Assert.AreEqual(PluginFieldKind.Text, detail.SubFields[0].FieldType);

            var resultDetail = function.ResultFields.Single(x => x.Key == "echoDetails");
            Assert.AreEqual(PluginFieldKind.TableForm, resultDetail.FieldType);
            Assert.AreEqual(3, resultDetail.SubFields.Count);
        }

        [TestMethod]
        public void Description_Rejects_Legacy_TableForm_Input()
        {
            using var plugin = new LegacyTableFormPlugin();

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => _ = plugin.Description);
            StringAssert.Contains(exception.Message, "PluginSubList");
        }

        [TestMethod]
        public void Description_Builds_Four_SubLists()
        {
            using var plugin = new FourSubListPlugin();

            var inputFields = plugin.Description.Functions.Single().InputFields;

            Assert.AreEqual(4, inputFields.Count(x => x.FieldType == PluginFieldKind.TableForm));
            CollectionAssert.AreEqual(
                new[] { "lines1", "lines2", "lines3", "lines4" },
                inputFields.Where(x => x.FieldType == PluginFieldKind.TableForm).Select(x => x.Key).ToArray());
        }

        [TestMethod]
        public void Description_Allows_Unused_SubList_GenericArgument()
        {
            using var plugin = new MissingSubListPropertyPlugin();

            var inputFields = plugin.Description.Functions.Single().InputFields;

            Assert.AreEqual(1, inputFields.Count(x => x.FieldType == PluginFieldKind.TableForm));
            Assert.AreEqual("lines1", inputFields.Single(x => x.FieldType == PluginFieldKind.TableForm).Key);
        }

        [TestMethod]
        public void Execute_Binds_Strong_Value_Types_And_Projects_Result_Keys()
        {
            TestJsonOptions.UseProjectDefaults();
            using var plugin = new AttributePlugin();
            var args = new JsonObject
            {
                ["status"] = "paid",
                ["owner"] = new JsonObject
                {
                    ["id"] = "u1",
                    ["value"] = "E001",
                    ["label"] = "Alice",
                    ["type"] = 2,
                },
                ["dept"] = new JsonObject
                {
                    ["id"] = "d1",
                    ["value"] = "D001",
                    ["label"] = "Finance",
                    ["type"] = 1,
                },
                ["tags"] = new JsonArray
                {
                    "urgent",
                    "finance",
                },
                ["attachment"] = "/files/receipt.pdf",
            };

            var result = plugin.Execute(
                new PluginSetting(),
                new PluginExecArgs { FunName = "Echo", FunArgs = args.ToJsonString() });

            Assert.AreEqual(0, result.Code);
            var projected = (IDictionary<string, object?>)result.Result!;
            Assert.AreEqual("paid", projected["statusValue"]);
            Assert.AreEqual("u1", projected["ownerId"]);
            Assert.AreEqual(2, projected["ownerType"]);
            Assert.AreEqual("D001", projected["deptValue"]);
            Assert.AreEqual("/files/receipt.pdf", projected["attachmentUrl"]);
            Assert.AreEqual(2, projected["tagCount"]);
        }

        [Plugin("attribute-plugin", "Attribute Plugin", Version = "1.2")]
        private sealed class AttributePlugin : PluginBase<AttributePluginSetting>
        {
            [PluginFunction("Echo", "Echo")]
            private EchoResult Echo(EchoArgs args)
            {
                return new EchoResult
                {
                    StatusValue = args.Status,
                    OwnerId = args.Owner?.Id,
                    OwnerType = args.Owner?.Type,
                    DeptValue = args.Dept?.Value,
                    AttachmentUrl = args.Attachment,
                    TagCount = args.Tags.Count,
                };
            }
        }

        private sealed class AttributePluginSetting
        {
        }

        private sealed class EchoArgs : PluginField
        {
            [PluginInput("Status", PluginFieldKind.SingleSelect, Key = "status")]
            public string? Status { get; set; }

            [PluginInput("Owner", PluginFieldKind.SingleEmployee, Key = "owner")]
            public EmployeeRef? Owner { get; set; }

            [PluginInput("Department", PluginFieldKind.SingleDepartment, Key = "dept")]
            public DepartmentRef? Dept { get; set; }

            [PluginInput("Tags", PluginFieldKind.MultipleSelect, Key = "tags")]
            public List<string> Tags { get; set; } = [];

            [PluginInput("Attachment", PluginFieldKind.FileUpload, Key = "attachment")]
            public string? Attachment { get; set; }
        }

        private sealed class EchoResult : PluginField
        {
            [PluginOutput("Status Value", PluginFieldKind.Text, Key = "statusValue")]
            public string? StatusValue { get; set; }

            [PluginOutput("Owner ID", PluginFieldKind.Text, Key = "ownerId")]
            public string? OwnerId { get; set; }

            [PluginOutput("Owner Type", PluginFieldKind.Number, Key = "ownerType")]
            public int? OwnerType { get; set; }

            [PluginOutput("Department Value", PluginFieldKind.Text, Key = "deptValue")]
            public string? DeptValue { get; set; }

            [PluginOutput("Attachment Url", PluginFieldKind.Text, Key = "attachmentUrl")]
            public string? AttachmentUrl { get; set; }

            [PluginOutput("Tag Count", PluginFieldKind.Number, Key = "tagCount")]
            public int TagCount { get; set; }
        }

        [Plugin("sub-list-plugin", "Sub List Plugin")]
        private sealed class SubListPlugin : PluginBase<AttributePluginSetting>
        {
            [PluginFunction("Echo", "Echo")]
            private SubListResult Echo(SubListArgs args)
            {
                return new SubListResult { EchoDetails = args.PaymentDetails };
            }
        }

        private sealed class SubListArgs : PluginSubList<PaymentDetail>
        {
            [PluginInput("Title", PluginFieldKind.Text, Key = "title")]
            public string? Title { get; set; }

            [PluginSubList("付款详情", Key = "paymentDetails")]
            public List<PaymentDetail> PaymentDetails { get; set; } = [];
        }

        private sealed class SubListResult : PluginSubList<PaymentDetail>
        {
            [PluginSubList("回显详情", Key = "echoDetails")]
            public List<PaymentDetail> EchoDetails { get; set; } = [];
        }

        private sealed class PaymentDetail : PluginField
        {
            [PluginInput("收方户名", PluginFieldKind.Text, Key = "payeeName")]
            [PluginOutput("收方户名", PluginFieldKind.Text, Key = "payeeName")]
            public string? PayeeName { get; set; }

            [PluginInput("收方账号", PluginFieldKind.Text, Key = "accountNo")]
            [PluginOutput("收方账号", PluginFieldKind.Text, Key = "accountNo")]
            public string? AccountNo { get; set; }

            [PluginInput("款项用途", PluginFieldKind.Text, Key = "purpose")]
            [PluginOutput("款项用途", PluginFieldKind.Text, Key = "purpose")]
            public string? Purpose { get; set; }
        }

        [Plugin("legacy-tableform-plugin", "Legacy TableForm Plugin")]
        private sealed class LegacyTableFormPlugin : PluginBase<AttributePluginSetting>
        {
            [PluginFunction("Echo", "Echo")]
            private EchoResult Echo(LegacyTableFormArgs args)
            {
                return new EchoResult();
            }
        }

        private sealed class LegacyTableFormArgs : PluginField
        {
            [PluginInput("Items", PluginFieldKind.TableForm, Key = "items")]
            public List<PaymentDetail> Items { get; set; } = [];
        }

        [Plugin("four-sub-list-plugin", "Four Sub List Plugin")]
        private sealed class FourSubListPlugin : PluginBase<AttributePluginSetting>
        {
            [PluginFunction("Echo", "Echo")]
            private EchoResult Echo(FourSubListArgs args)
            {
                return new EchoResult();
            }
        }

        private sealed class FourSubListArgs : PluginSubList<Line1, Line2, Line3, Line4>
        {
            [PluginSubList("Lines1", Key = "lines1")]
            public List<Line1> Lines1 { get; set; } = [];

            [PluginSubList("Lines2", Key = "lines2")]
            public List<Line2> Lines2 { get; set; } = [];

            [PluginSubList("Lines3", Key = "lines3")]
            public List<Line3> Lines3 { get; set; } = [];

            [PluginSubList("Lines4", Key = "lines4")]
            public List<Line4> Lines4 { get; set; } = [];
        }

        [Plugin("missing-sub-list-property-plugin", "Missing Sub List Property Plugin")]
        private sealed class MissingSubListPropertyPlugin : PluginBase<AttributePluginSetting>
        {
            [PluginFunction("Echo", "Echo")]
            private EchoResult Echo(MissingSubListPropertyArgs args)
            {
                return new EchoResult();
            }
        }

        private sealed class MissingSubListPropertyArgs : PluginSubList<Line1, Line2>
        {
            [PluginSubList("Lines1", Key = "lines1")]
            public List<Line1> Lines1 { get; set; } = [];
        }

        private sealed class Line1 : PluginField
        {
            [PluginInput("Value", PluginFieldKind.Text, Key = "value")]
            public string? Value { get; set; }
        }

        private sealed class Line2 : PluginField
        {
            [PluginInput("Value", PluginFieldKind.Text, Key = "value")]
            public string? Value { get; set; }
        }

        private sealed class Line3 : PluginField
        {
            [PluginInput("Value", PluginFieldKind.Text, Key = "value")]
            public string? Value { get; set; }
        }

        private sealed class Line4 : PluginField
        {
            [PluginInput("Value", PluginFieldKind.Text, Key = "value")]
            public string? Value { get; set; }
        }
    }
}
