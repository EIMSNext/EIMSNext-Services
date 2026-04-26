using System.Text.Json;
using System.Text.Json.Nodes;
using EIMSNext.Print.Abstractions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace EIMSNext.Print.Pdf
{
    public class PdfGenerator : BasePrintGenerator<PdfGenerator>
    {
        protected override Stream Generate(PrintTemplate template, PrintOption option, List<JsonObject> datas)
        {
            if (string.IsNullOrEmpty(template.Content)) return Stream.Null;

            var renderOptions = new PdfRenderOptions();

            UniverWorkbook? workbook = JsonSerializer.Deserialize<UniverWorkbook>(template.Content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (workbook == null || workbook.Sheets?.Count == 0) return Stream.Null;

            var worksheet = workbook.Sheets!.Values.First();

            PdfDocumentInitializer.InitializeFonts(renderOptions);

            var document = new Document();
            PdfDocumentInitializer.InitializeDocumentDefaults(document, renderOptions);

            var section = document.AddSection();
            ApplyPageSetup(section, worksheet, renderOptions);

            using var temporaryFileSession = new PdfTemporaryFileSession(renderOptions.TemporaryDirectory);
            RenderTables(document, workbook, worksheet, datas, option, renderOptions, temporaryFileSession);

            var ms = new MemoryStream();
            var renderer = new PdfDocumentRenderer()
            {
                Document = document
            };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(ms);
            ms.Position = 0;

            return ms;
        }

        private void RenderTables(Document document, UniverWorkbook workbook, UniverWorksheet worksheet, List<JsonObject> datas, PrintOption option, PdfRenderOptions renderOptions, PdfTemporaryFileSession temporaryFileSession)
        {
            if (datas == null || datas.Count == 0) return;

            var imageRenderer = new PdfImageRenderer(renderOptions, temporaryFileSession);

            for (int i = 0; i < datas.Count; i++)
            {
                if (i > 0)
                {
                    document.AddSection();
                    ApplyPageSetup(document.LastSection, worksheet, renderOptions);
                }

                var table = document.LastSection.AddTable();

                var tableGenerator = new PdfTableGenerator(workbook, renderOptions, IsPreview);
                tableGenerator.Generate(worksheet, table, datas[i], document.LastSection.PageSetup);
                imageRenderer.RenderImages(document.LastSection, workbook, worksheet);

                if (i < datas.Count - 1)
                {
                    document.LastSection.AddPageBreak();
                }
            }
        }

        private static void ApplyPageSetup(Section section, UniverWorksheet worksheet, PdfRenderOptions renderOptions)
        {
            var defaultPageSetup = section.Document?.DefaultPageSetup ?? section.PageSetup;
            section.PageSetup = defaultPageSetup.Clone();

            var templatePageSetup = worksheet.PageSetup;

            section.PageSetup.PageFormat = ResolvePageFormat(templatePageSetup, renderOptions);
            section.PageSetup.Orientation = ResolveOrientation(templatePageSetup, renderOptions);
            section.PageSetup.TopMargin = ResolveMargin(templatePageSetup?.TopMargin, renderOptions.PageTopMargin);
            section.PageSetup.BottomMargin = ResolveMargin(templatePageSetup?.BottomMargin, renderOptions.PageBottomMargin);
            section.PageSetup.LeftMargin = ResolveMargin(templatePageSetup?.LeftMargin, renderOptions.PageLeftMargin);
            section.PageSetup.RightMargin = ResolveMargin(templatePageSetup?.RightMargin, renderOptions.PageRightMargin);
        }

        private static PageFormat ResolvePageFormat(UniverPageSetup? pageSetup, PdfRenderOptions renderOptions)
        {
            var pageFormat = pageSetup?.PaperSize;
            if (string.IsNullOrWhiteSpace(pageFormat))
            {
                return renderOptions.PageFormat;
            }

            return pageFormat.Trim().ToLowerInvariant() switch
            {
                "a3" => PageFormat.A3,
                "a5" => PageFormat.A5,
                "letter" => PageFormat.Letter,
                "legal" => PageFormat.Legal,
                _ => PageFormat.A4
            };
        }

        private static Orientation ResolveOrientation(UniverPageSetup? pageSetup, PdfRenderOptions renderOptions)
        {
            if (pageSetup?.Landscape == true)
            {
                return Orientation.Landscape;
            }

            if (pageSetup?.Landscape == false && string.IsNullOrWhiteSpace(pageSetup.Orientation))
            {
                return Orientation.Portrait;
            }

            return pageSetup?.Orientation?.Trim().ToLowerInvariant() switch
            {
                "landscape" => Orientation.Landscape,
                "portrait" => Orientation.Portrait,
                _ => renderOptions.Orientation
            };
        }

        private static Unit ResolveMargin(double? margin, Unit fallback)
        {
            return margin.HasValue && margin.Value >= 0 ? Unit.FromPoint(margin.Value) : fallback;
        }
    }
}
