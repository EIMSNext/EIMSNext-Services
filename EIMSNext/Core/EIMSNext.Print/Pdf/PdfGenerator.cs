using System.Text.Json;
using System.Text.Json.Nodes;
using EIMSNext.Print.Common;
using MigraDoc;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace EIMSNext.Print.Pdf
{
    public class PdfGenerator : BasePrintGenerator<PdfGenerator>
    {
        protected override byte[] Generate(PrintTemplate template, PrintOption option, List<JsonObject> datas)
        {
            if (string.IsNullOrEmpty(template.Content)) return Array.Empty<byte>();

            var renderOptions = new PdfRenderOptions();

            UniverWorkbook? workbook = JsonSerializer.Deserialize<UniverWorkbook>(template.Content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (workbook == null || workbook.Sheets?.Count == 0) return Array.Empty<byte>();

            var worksheet = workbook.Sheets!.Values.First();

            #region 初始化文档

            FontsCache.Initialize();
            PdfFontResolverRuntime.Configure(renderOptions);
            PredefinedFontsAndChars.ErrorFontName = renderOptions.DefaultFontFamily;
            GlobalFontSettings.FontResolver = new FontResolver();
            GlobalFontSettings.FallbackFontResolver = new FallbackFontResolver();

            var document = new Document();
            var normalStyle = document.Styles[StyleNames.Normal];
            normalStyle!.Font.Name = renderOptions.DefaultFontFamily;
            normalStyle.Font.Size = renderOptions.DefaultFontSize;
            normalStyle.Font.Color = renderOptions.DefaultFontColor;

            var section = document.AddSection();
            ApplyPageSetup(section, worksheet, renderOptions);

            #endregion

            using var temporaryFileSession = new PdfTemporaryFileSession(renderOptions.TemporaryDirectory);
            RenderTables(document, workbook, worksheet, datas, option, renderOptions, temporaryFileSession);

            using var ms = new MemoryStream();
            var renderer = new PdfDocumentRenderer()
            {
                Document = document
            };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(ms);

            return ms.ToArray();
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

                // 如果不是最后一个数据，添加分页符
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

            var templatePageSetup = worksheet.PageSetup ?? worksheet.PrintSetup;

            section.PageSetup.PageFormat = ResolvePageFormat(templatePageSetup, renderOptions);
            section.PageSetup.Orientation = ResolveOrientation(templatePageSetup, renderOptions);
            section.PageSetup.TopMargin = ResolveMargin(templatePageSetup?.TopMargin ?? templatePageSetup?.MarginTop, renderOptions.PageTopMargin);
            section.PageSetup.BottomMargin = ResolveMargin(templatePageSetup?.BottomMargin ?? templatePageSetup?.MarginBottom, renderOptions.PageBottomMargin);
            section.PageSetup.LeftMargin = ResolveMargin(templatePageSetup?.LeftMargin ?? templatePageSetup?.MarginLeft, renderOptions.PageLeftMargin);
            section.PageSetup.RightMargin = ResolveMargin(templatePageSetup?.RightMargin ?? templatePageSetup?.MarginRight, renderOptions.PageRightMargin);
        }

        private static PageFormat ResolvePageFormat(UniverPageSetup? pageSetup, PdfRenderOptions renderOptions)
        {
            var pageFormat = pageSetup?.PageFormat ?? pageSetup?.PageSize ?? pageSetup?.PaperSize;
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
