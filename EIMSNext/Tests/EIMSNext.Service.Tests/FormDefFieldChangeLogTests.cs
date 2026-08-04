using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class FormDefFieldChangeLogTests
    {
        [TestMethod]
        public void ReconcileFieldChangeLogs_DeletedSubformAndColumn_UsesQualifiedPaths()
        {
            var oldContent = new FormContent
            {
                Items =
                [
                    new FieldDef
                    {
                        Field = "orders",
                        Type = FieldType.TableForm,
                        Title = "子表单",
                        Columns =
                        [
                            new FieldDef { Field = "remark", Type = FieldType.TextArea, Title = "多行文本" }
                        ]
                    }
                ]
            };
            var newContent = new FormContent { Items = [] };
            var deletedBy = new Operator("employee-1", "E001", "李里1");

            FormDefService.ReconcileFieldChangeLogs(oldContent, newContent, deletedBy, 1234);

            Assert.AreEqual(2, newContent.FieldChangeLogs.Count);
            var parent = newContent.FieldChangeLogs.Single(x => x.FieldId == "orders");
            Assert.AreEqual("子表单", parent.FieldLabel);
            var child = newContent.FieldChangeLogs.Single(x => x.FieldId == "orders>remark");
            Assert.AreEqual("子表单.多行文本", child.FieldLabel);
            Assert.AreEqual(FieldType.TextArea, child.FieldType);
            Assert.AreSame(deletedBy, child.DeletedBy);
            Assert.AreEqual(1234, child.DeletedTime);
        }

        [TestMethod]
        public void ReconcileFieldChangeLogs_RestoredSubfield_RemovesOnlyMatchingLog()
        {
            var oldContent = new FormContent
            {
                Items =
                [
                    new FieldDef { Field = "orders", Type = FieldType.TableForm, Title = "子表单", Columns = [] }
                ],
                FieldChangeLogs =
                [
                    new FieldChangeLog { FieldId = "orders", FieldType = FieldType.TableForm, FieldLabel = "子表单", DeletedTime = 1 },
                    new FieldChangeLog { FieldId = "orders>remark", FieldType = FieldType.TextArea, FieldLabel = "子表单.多行文本", DeletedTime = 2 },
                    new FieldChangeLog { FieldId = "other", FieldType = FieldType.Input, FieldLabel = "其他", DeletedTime = 3 }
                ]
            };
            var newContent = new FormContent
            {
                Items =
                [
                    new FieldDef
                    {
                        Field = "orders",
                        Type = FieldType.TableForm,
                        Title = "子表单",
                        Columns =
                        [
                            new FieldDef { Field = "remark", Type = FieldType.TextArea, Title = "多行文本" }
                        ]
                    }
                ]
            };

            FormDefService.ReconcileFieldChangeLogs(oldContent, newContent, Operator.Empty, 10);

            Assert.AreEqual(1, newContent.FieldChangeLogs.Count);
            Assert.AreEqual("other", newContent.FieldChangeLogs[0].FieldId);
        }

        [TestMethod]
        public void ReconcileFieldChangeLogs_IgnoresSubmittedMetadataAndKeepsNewestServerLog()
        {
            var oldContent = new FormContent
            {
                Items = [],
                FieldChangeLogs =
                [
                    new FieldChangeLog { FieldId = "name", FieldType = FieldType.Input, FieldLabel = "名称", DeletedTime = 20 },
                    new FieldChangeLog { FieldId = "name", FieldType = FieldType.TextArea, FieldLabel = "伪造记录", DeletedTime = 10 }
                ]
            };
            var newContent = new FormContent
            {
                Items = [],
                FieldChangeLogs =
                [
                    new FieldChangeLog { FieldId = "injected", FieldType = FieldType.Input, FieldLabel = "注入", DeletedTime = 999 }
                ]
            };

            FormDefService.ReconcileFieldChangeLogs(oldContent, newContent, Operator.Empty, 30);

            Assert.AreEqual(1, newContent.FieldChangeLogs.Count);
            Assert.AreEqual("name", newContent.FieldChangeLogs[0].FieldId);
            Assert.AreEqual("名称", newContent.FieldChangeLogs[0].FieldLabel);
            Assert.AreEqual(20, newContent.FieldChangeLogs[0].DeletedTime);
        }
    }
}
