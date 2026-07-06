using EIMSNext.Async.Tasks;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class FormDataImportCellConverterTests
    {
        // ===== ConvertNumber =====

        [TestMethod]
        public void ConvertNumber_TextualDigits_ParsesDouble()
        {
            Assert.AreEqual(123.0, (double)ImportCellConverters.ConvertNumber(null, "123"));
        }

        [TestMethod]
        public void ConvertNumber_InvalidText_ThrowsFormatException()
        {
            Assert.ThrowsExactly<FormatException>(() => ImportCellConverters.ConvertNumber(null, "abc"));
        }

        [TestMethod]
        public void ConvertNumber_NumericCell_ReturnsRaw()
        {
            var cell = NewNumericCell(42.5);
            var value = ImportCellConverters.ConvertNumber(cell, "42.5");
            Assert.AreEqual(42.5, (double)value, 0.0001);
        }

        // ===== ConvertTimestamp =====

        [TestMethod]
        public void ConvertTimestamp_StandardText_ParsesToUnixMs()
        {
            var ms = (long)ImportCellConverters.ConvertTimestamp(null, "2024-01-01 00:00:00");
            var expected = new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local)).ToUnixTimeMilliseconds();
            Assert.AreEqual(expected, ms);
        }

        [TestMethod]
        public void ConvertTimestamp_InvalidText_Throws()
        {
            Assert.ThrowsExactly<FormatException>(() => ImportCellConverters.ConvertTimestamp(null, "not-a-date"));
        }

        // ===== ConvertSingleOption =====

        [TestMethod]
        public void ConvertSingleOption_ValueMatch_ReturnsValue()
        {
            var field = NewFieldWithOptions("a", "苹果", "b", "香蕉");
            Assert.AreEqual("a", ImportCellConverters.ConvertSingleOption("a", field));
        }

        [TestMethod]
        public void ConvertSingleOption_LabelMatch_ReturnsValue()
        {
            var field = NewFieldWithOptions("a", "苹果", "b", "香蕉");
            Assert.AreEqual("a", ImportCellConverters.ConvertSingleOption("苹果", field));
        }

        [TestMethod]
        public void ConvertSingleOption_LabelMatch_CaseInsensitive()
        {
            var field = NewFieldWithOptions("a", "Apple", "b", "Banana");
            Assert.AreEqual("a", ImportCellConverters.ConvertSingleOption("APPLE", field));
        }

        [TestMethod]
        public void ConvertSingleOption_NoMatchWithOptions_Throws()
        {
            var field = NewFieldWithOptions("a", "苹果");
            Assert.ThrowsExactly<FormatException>(() => ImportCellConverters.ConvertSingleOption("x", field));
        }

        [TestMethod]
        public void ConvertSingleOption_NoMatchWithoutOptions_ReturnsText()
        {
            var field = new FieldDef { Field = "f", Type = FieldType.Radio };
            Assert.AreEqual("x", ImportCellConverters.ConvertSingleOption("x", field));
        }

        // ===== ConvertMultiOption / SplitMultiValue =====

        [TestMethod]
        public void SplitMultiValue_SupportsAllDelimiters()
        {
            var result = ImportCellConverters.SplitMultiValue("a,b，c;d；e\nf\rg");
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c", "d", "e", "f", "g" }, result);
        }

        [TestMethod]
        public void ConvertMultiOption_ValidItems_ReturnsListOfValues()
        {
            var field = NewFieldWithOptions("a", "A", "b", "B");
            var result = (List<string>)ImportCellConverters.ConvertMultiOption("a,b", field);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, result);
        }

        [TestMethod]
        public void ConvertMultiOption_InvalidItem_Throws()
        {
            var field = NewFieldWithOptions("a", "A", "b", "B");
            Assert.ThrowsExactly<FormatException>(() => ImportCellConverters.ConvertMultiOption("a,x", field));
        }

        [TestMethod]
        public void ConvertMultiOption_NoOptions_ReturnsListOfStrings()
        {
            var field = new FieldDef { Field = "f", Type = FieldType.CheckBox };
            var result = (List<string>)ImportCellConverters.ConvertMultiOption("a,b", field);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, result);
        }

        // ===== ConvertUrlList =====

        [TestMethod]
        public void ConvertUrlList_Single_ReturnsString()
        {
            Assert.AreEqual("http://x", ImportCellConverters.ConvertUrlList("http://x"));
        }

        [TestMethod]
        public void ConvertUrlList_Multiple_ReturnsList()
        {
            var result = (List<string>)ImportCellConverters.ConvertUrlList("http://a,http://b");
            CollectionAssert.AreEquivalent(new[] { "http://a", "http://b" }, result);
        }

        // ===== ConvertTextOrJson =====

        [TestMethod]
        public void ConvertTextOrJson_PlainText_ReturnsText()
        {
            Assert.AreEqual("hello", ImportCellConverters.ConvertTextOrJson("hello"));
        }

        [TestMethod]
        public void ConvertTextOrJson_ValidJsonObject_ReturnsObject()
        {
            var result = ImportCellConverters.ConvertTextOrJson("{\"a\":1}");
            Assert.IsNotNull(result);
            Assert.IsNotInstanceOfType(result, typeof(string));
        }

        [TestMethod]
        public void ConvertTextOrJson_InvalidJsonObject_ReturnsText()
        {
            Assert.AreEqual("{not json}", ImportCellConverters.ConvertTextOrJson("{not json}"));
        }

        // ===== ConvertEditableNumber / ConvertEditableTimestamp =====

        [TestMethod]
        public void ConvertEditableNumber_NumericTypes_ReturnAsIs()
        {
            Assert.AreEqual((byte)5, Convert.ToInt32(ImportCellConverters.ConvertEditableNumber((byte)5)));
            Assert.AreEqual((short)5, Convert.ToInt32(ImportCellConverters.ConvertEditableNumber((short)5)));
            Assert.AreEqual(5, Convert.ToInt32(ImportCellConverters.ConvertEditableNumber(5)));
            Assert.AreEqual(5L, (long)ImportCellConverters.ConvertEditableNumber(5L));
            Assert.AreEqual(5.5, (double)ImportCellConverters.ConvertEditableNumber(5.5), 0.0001);
        }

        [TestMethod]
        public void ConvertEditableNumber_String_Parses()
        {
            Assert.AreEqual(5.0, (double)ImportCellConverters.ConvertEditableNumber("5"), 0.0001);
        }

        [TestMethod]
        public void ConvertEditableNumber_Invalid_Throws()
        {
            Assert.ThrowsExactly<FormatException>(() => ImportCellConverters.ConvertEditableNumber("abc"));
        }

        [TestMethod]
        public void ConvertEditableTimestamp_LongMs_ReturnsAsIs()
        {
            Assert.AreEqual(1700000000000L, (long)ImportCellConverters.ConvertEditableTimestamp(1700000000000L));
        }

        [TestMethod]
        public void ConvertEditableTimestamp_LargeDouble_TreatedAsMs()
        {
            Assert.AreEqual(1700000000000L, (long)ImportCellConverters.ConvertEditableTimestamp(1700000000000.0));
        }

        // ===== IsEmpty / UnwrapJsonValue =====

        [TestMethod]
        public void IsEmpty_NullEmptyWhitespace_AllTrue()
        {
            Assert.IsTrue(ImportCellConverters.IsEmpty(null));
            Assert.IsTrue(ImportCellConverters.IsEmpty(""));
            Assert.IsTrue(ImportCellConverters.IsEmpty("   "));
        }

        [TestMethod]
        public void IsEmpty_NonEmptyString_False()
        {
            Assert.IsFalse(ImportCellConverters.IsEmpty("x"));
        }

        [TestMethod]
        public void IsEmpty_EmptyList_True()
        {
            Assert.IsTrue(ImportCellConverters.IsEmpty(new List<string>()));
        }

        [TestMethod]
        public void IsEmpty_NonEmptyList_False()
        {
            Assert.IsFalse(ImportCellConverters.IsEmpty(new List<string> { "a" }));
        }

        // ===== helpers =====

        private static FieldDef NewFieldWithOptions(string val1, string lbl1, string? val2 = null, string? lbl2 = null)
        {
            var opts = new List<ValueOption> { new() { Value = val1, Label = lbl1 } };
            if (val2 != null) opts.Add(new() { Value = val2, Label = lbl2 ?? val2 });
            return new FieldDef
            {
                Field = "f",
                Type = FieldType.Radio,
                Props = new FieldProp { Options = opts }
            };
        }

        private static ICell NewNumericCell(double value)
        {
            var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet();
            var row = sheet.CreateRow(0);
            var cell = row.CreateCell(0);
            cell.SetCellValue(value);
            return cell;
        }
    }
}
