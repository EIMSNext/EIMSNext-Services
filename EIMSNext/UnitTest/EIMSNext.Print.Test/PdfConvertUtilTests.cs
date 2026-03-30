using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Test
{
    [TestClass]
    public class PdfConvertUtilTests
    {
        [TestMethod]
        public void HexToMigraColor_ShouldReturnFallback_WhenColorInvalid()
        {
            var color = Pdf.PdfConvertUtil.HexToMigraColor("invalid", Colors.Red);

            Assert.AreEqual(Colors.Red, color);
        }

        [TestMethod]
        public void HAlignToMigra_ShouldSupportNumericValues()
        {
            var alignment = Pdf.PdfConvertUtil.HAlignToMigra("3", ParagraphAlignment.Left);

            Assert.AreEqual(ParagraphAlignment.Right, alignment);
        }

        [TestMethod]
        public void VAlignToMigra_ShouldSupportEnumValues()
        {
            var alignment = Pdf.PdfConvertUtil.VAlignToMigra(Pdf.UniverVerticalAlignment.Middle, VerticalAlignment.Top);

            Assert.AreEqual(VerticalAlignment.Center, alignment);
        }
    }
}
