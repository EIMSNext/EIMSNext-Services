using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class FormDataImportValidatorTests
    {
        // ===== ValidateImportFile =====

        [TestMethod]
        public void ValidateImportFile_XlsxUnder20Mb_Passes()
        {
            FormDataApiService.ValidateImportFile("a.xlsx", 20L * 1024 * 1024);
        }

        [TestMethod]
        public void ValidateImportFile_XlsxOver20Mb_Throws()
        {
            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                FormDataApiService.ValidateImportFile("a.xlsx", 20L * 1024 * 1024 + 1));
            StringAssert.Contains(ex.Message, "20MB");
        }

        [TestMethod]
        public void ValidateImportFile_XlsUnder5Mb_Passes()
        {
            FormDataApiService.ValidateImportFile("a.xls", 5L * 1024 * 1024);
        }

        [TestMethod]
        public void ValidateImportFile_XlsOver5Mb_Throws()
        {
            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                FormDataApiService.ValidateImportFile("a.xls", 5L * 1024 * 1024 + 1));
            StringAssert.Contains(ex.Message, "5MB");
        }

        [TestMethod]
        public void ValidateImportFile_Csv_Throws()
        {
            var ex = Assert.ThrowsExactly<ArgumentException>(() =>
                FormDataApiService.ValidateImportFile("a.csv", 1024));
            StringAssert.Contains(ex.Message, "xlsx");
        }

        [TestMethod]
        public void ValidateImportFile_ZeroSize_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                FormDataApiService.ValidateImportFile("a.xlsx", 0));
        }

        // ===== BuildImportFieldSnapshot =====

        [TestMethod]
        public void BuildImportFieldSnapshot_SkipsHiddenAndSignature()
        {
            var form = new FormDef
            {
                Content = new FormContent
                {
                    Items = new List<FieldDef>
                    {
                        NewField("title", FieldType.Input, "标题"),
                        NewField("hidden", FieldType.Input, "隐藏", hidden: true),
                        NewField("sig", FieldType.Signature, "签名"),
                    }
                }
            };

            var snap = FormDataApiService.BuildImportFieldSnapshot(form);

            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual("title", snap[0].Field);
            Assert.AreEqual("标题", snap[0].Title);
        }

        [TestMethod]
        public void BuildImportFieldSnapshot_ExpandsTableFormAsParentChild()
        {
            var form = new FormDef
            {
                Content = new FormContent
                {
                    Items = new List<FieldDef>
                    {
                        new FieldDef
                        {
                            Field = "items",
                            Title = "明细",
                            Type = FieldType.TableForm,
                            Columns = new List<FieldDef>
                            {
                                NewField("qty", FieldType.Number, "数量"),
                                NewField("price", FieldType.Number, "单价"),
                            }
                        }
                    }
                }
            };

            var snap = FormDataApiService.BuildImportFieldSnapshot(form);

            Assert.AreEqual(2, snap.Count);
            Assert.AreEqual("items>qty", snap[0].Field);
            Assert.AreEqual("明细.数量", snap[0].Title);
            Assert.AreEqual("items>price", snap[1].Field);
        }

        [TestMethod]
        public void BuildImportFieldSnapshot_RequiredTrueWhenEitherFieldOrPropsRequired()
        {
            var form = new FormDef
            {
                Content = new FormContent
                {
                    Items = new List<FieldDef>
                    {
                        NewField("a", FieldType.Input, "A", required: true),
                        NewField("b", FieldType.Input, "B", propsRequired: true),
                        NewField("c", FieldType.Input, "C"),
                    }
                }
            };

            var snap = FormDataApiService.BuildImportFieldSnapshot(form);
            Assert.IsTrue(snap.Single(x => x.Field == "a").Required);
            Assert.IsTrue(snap.Single(x => x.Field == "b").Required);
            Assert.IsFalse(snap.Single(x => x.Field == "c").Required);
        }

        [TestMethod]
        public void BuildImportFieldSnapshot_SkipsFieldsWithoutEditablePermission()
        {
            var form = new FormDef
            {
                Content = new FormContent
                {
                    Items = new List<FieldDef>
                    {
                        NewField("allowed", FieldType.Input, "可导入"),
                        NewField("readonly", FieldType.Input, "只读"),
                        NewField("hiddenByPerm", FieldType.Input, "不可见"),
                    }
                }
            };
            var fieldPerms = new List<FieldPerm>
            {
                new() { Id = "allowed", Visible = true, Editable = true },
                new() { Id = "readonly", Visible = true, Editable = false },
                new() { Id = "hiddenByPerm", Visible = false, Editable = true },
            };

            var snap = FormDataApiService.BuildImportFieldSnapshot(form, fieldPerms);

            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual("allowed", snap[0].Field);
        }

        [TestMethod]
        public void BuildImportFieldSnapshot_TableChildRequiresParentAndChildEditable()
        {
            var form = new FormDef
            {
                Content = new FormContent
                {
                    Items = new List<FieldDef>
                    {
                        new()
                        {
                            Field = "items",
                            Title = "明细",
                            Type = FieldType.TableForm,
                            Columns = new List<FieldDef>
                            {
                                NewField("qty", FieldType.Number, "数量"),
                                NewField("price", FieldType.Number, "单价"),
                            }
                        }
                    }
                }
            };
            var fieldPerms = new List<FieldPerm>
            {
                new() { Id = "items", Visible = true, Editable = true },
                new() { Id = "items>qty", Visible = true, Editable = true },
                new() { Id = "items>price", Visible = true, Editable = false },
            };

            var snap = FormDataApiService.BuildImportFieldSnapshot(form, fieldPerms);

            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual("items>qty", snap[0].Field);
        }

        [TestMethod]
        public void BuildImportFieldSnapshot_EmptyItems_ReturnsEmpty()
        {
            var form = new FormDef { Content = new FormContent { Items = new List<FieldDef>() } };
            var snap = FormDataApiService.BuildImportFieldSnapshot(form);
            Assert.AreEqual(0, snap.Count);
        }

        // ===== NormalizeFileName =====

        [TestMethod]
        public void NormalizeFileName_StripsPathAndReplacesInvalidChars()
        {
            var result = FormDataApiService.NormalizeFileName(@"C:\path\bad|name?.xlsx");
            Assert.IsFalse(result.Contains('\\'));
            Assert.IsFalse(result.Contains('|'));
            Assert.IsFalse(result.Contains('?'));
            Assert.EndsWith(".xlsx", result);
        }

        [TestMethod]
        public void NormalizeFileName_EmptyInput_ReturnsFallback()
        {
            var result = FormDataApiService.NormalizeFileName("");
            Assert.IsTrue(result.StartsWith("import_"));
            Assert.EndsWith(".xlsx", result);
        }

        private static FieldDef NewField(string field, string type, string title, bool hidden = false, bool required = false, bool? propsRequired = null)
        {
            return new FieldDef
            {
                Field = field,
                Type = type,
                Title = title,
                Hidden = hidden,
                Required = required,
                Props = new FieldProp { Required = propsRequired }
            };
        }
    }
}
