using System.Text.Json;
using System.Text.Json.Nodes;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Pdf
{
    internal sealed class PdfImageRenderer
    {
        private readonly PdfRenderOptions _options;
        private readonly PdfSheetLayoutCalculator _layoutCalculator;
        private readonly PdfTemporaryFileSession _temporaryFileSession;

        public PdfImageRenderer(PdfRenderOptions options, PdfTemporaryFileSession temporaryFileSession)
        {
            _options = options;
            _layoutCalculator = new PdfSheetLayoutCalculator(options);
            _temporaryFileSession = temporaryFileSession;
        }

        public void RenderImages(Section section, UniverWorkbook workbook, UniverWorksheet worksheet)
        {
            var images = ResolveWorksheetImages(workbook, worksheet);
            if (images.Count == 0)
            {
                return;
            }

            var renderPlan = _layoutCalculator.CreatePlan(worksheet, section.PageSetup);

            foreach (var image in images)
            {
                if (!TryResolveImagePath(image, _temporaryFileSession, out var imagePath))
                {
                    continue;
                }

                if (!TryResolveImageBounds(image, worksheet, section.PageSetup, renderPlan, out var leftCm, out var topCm, out var widthCm, out var heightCm))
                {
                    continue;
                }

                try
                {
                    var textFrame = section.AddTextFrame();

                    textFrame.Left = Unit.FromCentimeter(leftCm);
                    textFrame.Top = Unit.FromCentimeter(topCm);
                    textFrame.Width = Unit.FromCentimeter(widthCm > 0 ? widthCm : _options.DefaultColumnWidthCm);
                    textFrame.Height = Unit.FromCentimeter(heightCm > 0 ? heightCm : _options.DefaultRowHeightCm);
                    textFrame.LineFormat.Visible = false;

                    var imageShape = textFrame.AddImage(imagePath);
                    imageShape.LockAspectRatio = false;
                    imageShape.Width = textFrame.Width;
                    imageShape.Height = textFrame.Height;
                }
                catch
                {
                }
            }
        }

        public bool TryRenderCellImage(Cell cell, UniverWorkbook workbook, UniverWorksheet worksheet, UniverCell univerCell, int rowIndex, int columnIndex, double scaleFactor)
        {
            var cellImage = univerCell.Image ?? univerCell.InlineImage ?? ResolveCellImageFromValue(univerCell.Value);
            if (cellImage == null)
            {
                return false;
            }

            if (!TryResolveCellImagePath(cellImage, _temporaryFileSession, out var imagePath))
            {
                return false;
            }

            var paragraph = cell.AddParagraph();
            paragraph.Format.Alignment = cell.Format.Alignment;

            var image = paragraph.AddImage(imagePath);
            image.LockAspectRatio = false;

            var widthCm = ResolveCellImageWidthCm(worksheet, columnIndex, cellImage, scaleFactor);
            var heightCm = ResolveCellImageHeightCm(worksheet, rowIndex, cellImage);

            image.Width = Unit.FromCentimeter(widthCm);
            image.Height = Unit.FromCentimeter(heightCm);
            return true;
        }

        private static bool TryResolveImagePath(UniverImageData image, PdfTemporaryFileSession temporaryFileSession, out string imagePath)
        {
            imagePath = string.Empty;

            if (TryDecodeSource(image.Source, out var inlineBytes))
            {
                imagePath = temporaryFileSession.CreateFile(ResolveImageExtension(image.Source, image.ImageSourceType), inlineBytes);
                return true;
            }

            return false;
        }

        private static bool TryResolveCellImagePath(UniverCellImage image, PdfTemporaryFileSession temporaryFileSession, out string imagePath)
        {
            imagePath = string.Empty;

            if (TryDecodeSource(image.Source, out var inlineBytes))
            {
                imagePath = temporaryFileSession.CreateFile(ResolveImageExtension(image.Source, image.ImageSourceType ?? image.Type), inlineBytes);
                return true;
            }

            return false;
        }

        private bool TryResolveImageBounds(UniverImageData image, UniverWorksheet worksheet, PageSetup pageSetup, PdfSheetRenderPlan renderPlan, out double leftCm, out double topCm, out double widthCm, out double heightCm)
        {
            leftCm = 0;
            topCm = 0;
            widthCm = 0;
            heightCm = 0;

            var transform = image.AxisAlignSheetTransform ?? image.SheetTransform;
            if (transform?.From != null)
            {
                leftCm = pageSetup.LeftMargin.Centimeter + GetColumnOffsetCm(worksheet, transform.From.Column, transform.From.ColumnOffset, renderPlan.ScaleFactor);
                topCm = pageSetup.TopMargin.Centimeter + GetRowOffsetCm(worksheet, transform.From.Row, transform.From.RowOffset);

                if (transform.To != null)
                {
                    var rightCm = pageSetup.LeftMargin.Centimeter + GetColumnOffsetCm(worksheet, transform.To.Column, transform.To.ColumnOffset, renderPlan.ScaleFactor);
                    var bottomCm = pageSetup.TopMargin.Centimeter + GetRowOffsetCm(worksheet, transform.To.Row, transform.To.RowOffset);
                    widthCm = Math.Max(rightCm - leftCm, 0);
                    heightCm = Math.Max(bottomCm - topCm, 0);
                }
            }
            else
            {
                return false;
            }

            if (widthCm <= 0 && image.Width.HasValue)
            {
                widthCm = _layoutCalculator.MapHorizontalPixelsToCm(worksheet, image.Width.Value, renderPlan.ScaleFactor);
            }

            if (heightCm <= 0 && image.Height.HasValue)
            {
                heightCm = _layoutCalculator.MapVerticalPixelsToCm(worksheet, image.Height.Value);
            }

            return widthCm > 0 || heightCm > 0;
        }

        private static List<UniverImageData> ResolveWorksheetImages(UniverWorkbook workbook, UniverWorksheet worksheet)
        {
            var images = new List<UniverImageData>();
            var imageKeys = new HashSet<string>(StringComparer.Ordinal);

            void addImage(UniverImageData image)
            {
                var key = image.DrawingId ?? image.ImageId ?? image.Source;
                if (string.IsNullOrWhiteSpace(key) || imageKeys.Add(key))
                {
                    images.Add(image);
                }
            }

            if (worksheet.Images != null)
            {
                foreach (var image in worksheet.Images)
                {
                    if (image != null)
                    {
                        addImage(image);
                    }
                }
            }

            if (workbook.Resources == null)
            {
                return images;
            }

            foreach (var resource in workbook.Resources)
            {
                if (!string.Equals(resource.Name, "SHEET_DRAWING_PLUGIN", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(resource.Data))
                {
                    continue;
                }

                try
                {
                    var resourceNode = JsonNode.Parse(resource.Data);
                    if (resourceNode == null)
                    {
                        continue;
                    }

                    foreach (var image in EnumerateDrawingImages(resourceNode))
                    {
                        addImage(image);
                    }
                }
                catch
                {
                }
            }

            return images;
        }

        private static IEnumerable<UniverImageData> EnumerateDrawingImages(JsonNode node)
        {
            if (node is JsonObject jsonObject)
            {
                if (LooksLikeDrawingImage(jsonObject) && TryDeserializeImage(jsonObject, out var image))
                {
                    yield return image;
                }

                foreach (var property in jsonObject)
                {
                    var child = property.Value;
                    if (child == null)
                    {
                        continue;
                    }

                    foreach (var imageData in EnumerateDrawingImages(child))
                    {
                        yield return imageData;
                    }
                }
            }

            if (node is JsonArray jsonArray)
            {
                foreach (var child in jsonArray)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    foreach (var imageData in EnumerateDrawingImages(child))
                    {
                        yield return imageData;
                    }
                }
            }
        }

        private static bool LooksLikeDrawingImage(JsonObject jsonObject)
        {
            return jsonObject.ContainsKey("source")
                && (jsonObject.ContainsKey("sheetTransform") || jsonObject.ContainsKey("axisAlignSheetTransform"));
        }

        private static bool TryDeserializeImage(JsonObject jsonObject, out UniverImageData image)
        {
            image = null!;

            try
            {
                image = JsonSerializer.Deserialize<UniverImageData>(jsonObject.ToJsonString(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                })!;

                return image != null;
            }
            catch
            {
                return false;
            }
        }

        private double GetColumnOffsetCm(UniverWorksheet worksheet, int column, double columnOffset, double scaleFactor)
        {
            var pixels = 0d;
            for (var index = 0; index < column; index++)
            {
                pixels += GetColumnPixels(worksheet, index);
            }

            pixels += Math.Max(columnOffset, 0);
            return _layoutCalculator.MapHorizontalPixelsToCm(worksheet, pixels, scaleFactor);
        }

        private double GetRowOffsetCm(UniverWorksheet worksheet, int row, double rowOffset)
        {
            var pixels = 0d;
            for (var index = 0; index < row; index++)
            {
                pixels += GetRowPixels(worksheet, index);
            }

            pixels += Math.Max(rowOffset, 0);
            return _layoutCalculator.MapVerticalPixelsToCm(worksheet, pixels);
        }

        private static double GetColumnPixels(UniverWorksheet worksheet, int columnIndex)
        {
            if (worksheet.ColumnWidth?.TryGetValue(columnIndex, out var width) == true && width > 0)
            {
                return width;
            }

            if (worksheet.ColumnData?.TryGetValue(columnIndex.ToString(), out var columnData) == true)
            {
                if (columnData.InnerWidth.HasValue && columnData.InnerWidth.Value > 0)
                {
                    return columnData.InnerWidth.Value;
                }

                if (columnData.Width.HasValue && columnData.Width.Value > 0)
                {
                    return columnData.Width.Value;
                }
            }

            return worksheet.DefaultColumnWidth;
        }

        private static double GetRowPixels(UniverWorksheet worksheet, int rowIndex)
        {
            if (worksheet.RowHeight?.TryGetValue(rowIndex, out var height) == true && height > 0)
            {
                return height;
            }

            if (worksheet.RowData?.TryGetValue(rowIndex.ToString(), out var rowData) == true)
            {
                if (rowData.ActualHeight.HasValue && rowData.ActualHeight.Value > 0)
                {
                    return rowData.ActualHeight.Value;
                }

                if (rowData.Height.HasValue && rowData.Height.Value > 0)
                {
                    return rowData.Height.Value;
                }
            }

            return worksheet.DefaultRowHeight;
        }

        private static double ResolveCellImageWidthCm(UniverWorksheet worksheet, int columnIndex, UniverCellImage image, double scaleFactor)
        {
            if (image.Width.HasValue && image.Width.Value > 0)
            {
                return Math.Max(PdfConvertUtil.PixelToCm(image.Width.Value, PdfRenderDefaults.DefaultColumnWidthCm) * scaleFactor, 0.2);
            }

            return Math.Max(PdfConvertUtil.PixelToCm(GetColumnPixels(worksheet, columnIndex), PdfRenderDefaults.DefaultColumnWidthCm) * scaleFactor * 0.92, 0.2);
        }

        private static double ResolveCellImageHeightCm(UniverWorksheet worksheet, int rowIndex, UniverCellImage image)
        {
            if (image.Height.HasValue && image.Height.Value > 0)
            {
                return Math.Max(PdfConvertUtil.PixelToCm(image.Height.Value, PdfRenderDefaults.DefaultRowHeightCm), 0.2);
            }

            return Math.Max(PdfConvertUtil.PixelToCm(GetRowPixels(worksheet, rowIndex), PdfRenderDefaults.DefaultRowHeightCm) * 0.85, 0.2);
        }

        private static bool TryDecodeSource(string? source, out byte[] imageBytes)
        {
            imageBytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return TryDecodeResourceData(source, out imageBytes);
        }

        private static bool TryDecodeResourceData(string data, out byte[] imageBytes)
        {
            imageBytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(data))
            {
                return false;
            }

            var payload = data.Trim();
            var commaIndex = payload.IndexOf(',');
            if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
            {
                payload = payload[(commaIndex + 1)..];
            }

            try
            {
                imageBytes = Convert.FromBase64String(payload);
                return imageBytes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static UniverCellImage? ResolveCellImageFromValue(object? value)
        {
            if (value is not JsonElement element)
            {
                return null;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (TryDeserializeCellImage(element, out var cellImage))
            {
                return cellImage;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (TryDeserializeCellImage(property.Value, out cellImage))
                {
                    return cellImage;
                }
            }

            return null;
        }

        private static bool TryDeserializeCellImage(JsonElement element, out UniverCellImage? cellImage)
        {
            cellImage = null;
            if (!element.TryGetProperty("source", out _))
            {
                return false;
            }

            try
            {
                cellImage = JsonSerializer.Deserialize<UniverCellImage>(element.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                return cellImage != null;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveImageExtension(string? source, string? imageType)
        {
            if (!string.IsNullOrWhiteSpace(imageType))
            {
                if (imageType.Contains("png", StringComparison.OrdinalIgnoreCase)) return "png";
                if (imageType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || imageType.Contains("jpg", StringComparison.OrdinalIgnoreCase)) return "jpg";
                if (imageType.Contains("bmp", StringComparison.OrdinalIgnoreCase)) return "bmp";
                if (imageType.Contains("gif", StringComparison.OrdinalIgnoreCase)) return "gif";
            }

            if (!string.IsNullOrWhiteSpace(source) && source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (source.Contains("image/png", StringComparison.OrdinalIgnoreCase)) return "png";
                if (source.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase) || source.Contains("image/jpg", StringComparison.OrdinalIgnoreCase)) return "jpg";
                if (source.Contains("image/bmp", StringComparison.OrdinalIgnoreCase)) return "bmp";
                if (source.Contains("image/gif", StringComparison.OrdinalIgnoreCase)) return "gif";
            }

            return "png";
        }
    }
}
