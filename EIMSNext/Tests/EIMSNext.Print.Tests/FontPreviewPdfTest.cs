using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace EIMSNext.Print.Tests
{
    [TestClass]
    public class FontPreviewPdfTest
    {
        [TestMethod]
        public void GenerateFontPreviewPdf()
        {
            var outputDirectory = @"D:\Temp";
            Directory.CreateDirectory(outputDirectory);

            var renderOptions = new Pdf.PdfRenderOptions();
            Pdf.PdfDocumentInitializer.InitializeFonts(renderOptions);

            var document = new Document();
            Pdf.PdfDocumentInitializer.InitializeDocumentDefaults(document, renderOptions);

            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.Orientation = Orientation.Landscape;
            section.PageSetup.TopMargin = Unit.FromCentimeter(1);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1);

            section.AddParagraph("Font Preview").Format.Font.Size = 14;
            section.AddParagraph("Sample: abc ABC 123 测试 字体预览");

            var table = section.AddTable();
            table.Borders.Width = 0.5;
            table.Format.LeftIndent = 0;
            table.AddColumn(Unit.FromCentimeter(6.5));
            table.AddColumn(Unit.FromCentimeter(4));
            table.AddColumn(Unit.FromCentimeter(17));

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.Cells[0].AddParagraph("Resolved Font");
            header.Cells[1].AddParagraph("Style");
            header.Cells[2].AddParagraph("Preview");

            var fontFamilies = new[]
            {
                "Microsoft YaHei",
                "Microsoft YaHei UI",
                "SimSun",
                "NSimSun",
                "fangsong",
                "Arial",
                "Times New Roman",
                "Verdana",
                "Tahoma"
            };

            var variants = new[]
            {
                new FontVariant(false, false, false, "Normal"),
                new FontVariant(true, false, false, "Bold"),
                new FontVariant(false, true, false, "Italic"),
                new FontVariant(false, false, true, "Underline"),
                new FontVariant(true, true, false, "Bold + Italic")
            };

            foreach (var family in fontFamilies)
            {
                foreach (var variant in variants)
                {
                    var resolvedFamily = Pdf.PdfFontResolverRuntime.ResolveFontFamily(family, variant.Bold, variant.Italic);
                    var sampleText = $"Request={family} | Resolved={resolvedFamily} | {resolvedFamily} | {variant.Label} | abc ABC 123 测试 字体预览";
                    var previewFont = Pdf.PdfTextFontHelper.ResolveParagraphFontName(sampleText, resolvedFamily, variant.Bold);
                    var row = table.AddRow();
                    row.VerticalAlignment = VerticalAlignment.Center;
                    row.HeightRule = RowHeightRule.AtLeast;

                    row.Cells[0].AddParagraph($"{family} -> {resolvedFamily}");
                    row.Cells[1].AddParagraph(variant.Label);

                    var preview = row.Cells[2].AddParagraph($"PreviewFont={previewFont} | {sampleText}");
                    preview.Format.Font.Name = previewFont;
                    preview.Format.Font.Size = 12;
                    preview.Format.Font.Bold = variant.Bold;
                    preview.Format.Font.Italic = variant.Italic;
                    preview.Format.Font.Underline = variant.Underline ? Underline.Single : Underline.None;
                }
            }

            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();

            var filePath = Path.Combine(outputDirectory, $"font-preview-{Guid.NewGuid():N}.pdf");
            renderer.PdfDocument.Save(filePath);

            Assert.IsTrue(File.Exists(filePath));
            Assert.IsTrue(new FileInfo(filePath).Length > 0);
        }

        private sealed record FontVariant(bool Bold, bool Italic, bool Underline, string Label);
    }
}
