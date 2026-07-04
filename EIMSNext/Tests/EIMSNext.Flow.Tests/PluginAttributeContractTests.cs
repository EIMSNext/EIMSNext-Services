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

        private sealed class EchoArgs
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

        private sealed class EchoResult
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
    }
}
