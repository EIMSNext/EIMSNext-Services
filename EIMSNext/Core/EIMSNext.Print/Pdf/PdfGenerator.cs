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

            UniverWorkbook? workbook = JsonSerializer.Deserialize<UniverWorkbook>(template.Content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (workbook == null || workbook.Sheets?.Count == 0) return Array.Empty<byte>();

            var worksheet = workbook.Sheets!.Values.First();

            #region 初始化文档

            FontsCache.Initialize();
            PredefinedFontsAndChars.ErrorFontName = FallbackFontResolver.DefaultFontName;
            GlobalFontSettings.FontResolver = new FontResolver();
            GlobalFontSettings.FallbackFontResolver = new FallbackFontResolver();

            var document = new Document();
            var normalStyle = document.Styles[StyleNames.Normal];
            normalStyle!.Font.Name = FallbackFontResolver.DefaultFontName;

            var section = document.AddSection();
            //TODO:纸张应读取页面设置
            section.PageSetup=document.DefaultPageSetup.Clone();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.Orientation = Orientation.Portrait;

            #endregion

            // 处理数据数组，为每个数据生成一个表格，并添加分页符
            RenderTables(document, workbook, worksheet, datas, option);

            using var ms = new MemoryStream();
            var renderer = new PdfDocumentRenderer()
            {
                Document = document
            };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(ms);

            return ms.ToArray();
        }

        /// <summary>
        /// 处理数据数组，为每个数据生成一个表格，并添加分页符
        /// </summary>
        private void RenderTables(Document document, UniverWorkbook workbook, UniverWorksheet worksheet, List<JsonObject> datas, PrintOption option)
        {
            if (datas == null || datas.Count == 0) return;

            for (int i = 0; i < datas.Count; i++)
            {
                // 为每个数据创建一个表格
                var table = document.LastSection.AddTable();
              
                // 使用PdfTableGenerator生成表格
                var tableGenerator = new PdfTableGenerator(workbook, IsPreview);
                tableGenerator.Generate(worksheet, table, datas[i]);

                // 如果不是最后一个数据，添加分页符
                if (i < datas.Count - 1)
                {
                    document.LastSection.AddPageBreak();
                }
            }
        }
    }
}
