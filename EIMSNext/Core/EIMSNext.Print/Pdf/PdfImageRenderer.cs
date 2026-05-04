using System.Text.Json;
using System.Text.Json.Nodes;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Pdf
{
    internal sealed class PdfImageRenderer
    {
        private readonly PdfRenderOptions _options;
        private readonly PdfSheetLayoutCalculator _layoutCalculator;

        public PdfImageRenderer(PdfRenderOptions options)
        {
            _options = options;
            _layoutCalculator = new PdfSheetLayoutCalculator(options);
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
                if (!TryResolveImagePath(image, out var imagePath))
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

                    textFrame.RelativeHorizontal = RelativeHorizontal.Page;
                    textFrame.RelativeVertical = RelativeVertical.Page;
                    textFrame.Left = Unit.FromCentimeter(leftCm);
                    textFrame.Top = Unit.FromCentimeter(topCm);
                    textFrame.Width = Unit.FromCentimeter(widthCm > 0 ? widthCm : _options.DefaultColumnWidthCm);
                    textFrame.Height = Unit.FromCentimeter(heightCm > 0 ? heightCm : _options.DefaultRowHeightCm);
                    textFrame.LineFormat.Visible = false;
                    textFrame.WrapFormat.Style = WrapStyle.Through;

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
            var cellImage = univerCell.Image
                ?? univerCell.InlineImage
                ?? ResolveCellImageFromParagraph(univerCell)
                ?? ResolveCellImageFromValue(univerCell.Value);
            if (cellImage == null)
            {
                return false;
            }

            if (!TryResolveCellImagePath(workbook, cellImage, out var imagePath))
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

        private static bool TryResolveImagePath(UniverImageData image, out string imagePath)
        {
            imagePath = string.Empty;

            return TryConvertToMigraDocImageSource(image.Source, out imagePath);
        }

        private static UniverCellImage? ResolveCellImageFromParagraph(UniverCell cell)
        {
            if (cell.ParagraphContent.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!cell.ParagraphContent.TryGetProperty("drawings", out var drawings) || drawings.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in drawings.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var drawing = property.Value;
                if (!drawing.TryGetProperty("source", out var sourceElement) || sourceElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var cellImage = new UniverCellImage
                {
                    Source = sourceElement.GetString(),
                    ImageSourceType = TryGetStringProperty(drawing, "imageSourceType"),
                    ImageId = TryGetStringProperty(drawing, "imageId"),
                    Width = TryGetDoubleProperty(drawing, "width"),
                    Height = TryGetDoubleProperty(drawing, "height"),
                };

                if ((!cellImage.Width.HasValue || !cellImage.Height.HasValue) && drawing.TryGetProperty("transform", out var transformElement))
                {
                    cellImage.Width ??= TryGetDoubleProperty(transformElement, "width");
                    cellImage.Height ??= TryGetDoubleProperty(transformElement, "height");
                }

                return cellImage;
            }

            return null;
        }

        private static bool TryResolveCellImagePath(UniverWorkbook workbook, UniverCellImage image, out string imagePath)
        {
            imagePath = string.Empty;

            var source = ResolveCellImageSource(workbook, image);

            return TryConvertToMigraDocImageSource(source, out imagePath);
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

        private static string? ResolveCellImageSource(UniverWorkbook workbook, UniverCellImage image)
        {
            if (LooksLikeInlineImagePayload(image.Source))
            {
                return image.Source;
            }

            if (!string.IsNullOrWhiteSpace(image.ImageId)
                && TryResolveImageSourceById(workbook, image.ImageId, out var sourceByImageId))
            {
                return sourceByImageId;
            }

            if (!string.IsNullOrWhiteSpace(image.Source)
                && TryResolveImageSourceById(workbook, image.Source, out var sourceBySourceKey))
            {
                return sourceBySourceKey;
            }

            if (image.ExtraProperties != null)
            {
                if (TryGetString(image.ExtraProperties, "imageId", out var extraImageId)
                    && TryResolveImageSourceById(workbook, extraImageId, out var sourceByExtraImageId))
                {
                    return sourceByExtraImageId;
                }

                if (TryGetString(image.ExtraProperties, "src", out var extraSource) && LooksLikeInlineImagePayload(extraSource))
                {
                    return extraSource;
                }
            }

            return image.Source;
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

        private static bool TryResolveImageSourceById(UniverWorkbook workbook, string imageId, out string source)
        {
            source = string.Empty;
            if (string.IsNullOrWhiteSpace(imageId) || workbook.Resources == null)
            {
                return false;
            }

            foreach (var resource in workbook.Resources)
            {
                if (string.IsNullOrWhiteSpace(resource.Data))
                {
                    continue;
                }

                try
                {
                    var resourceNode = JsonNode.Parse(resource.Data);
                    if (resourceNode != null && TryFindImageSource(resourceNode, imageId, out source))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool TryFindImageSource(JsonNode node, string imageId, out string source)
        {
            source = string.Empty;

            if (node is JsonObject jsonObject)
            {
                if (TryGetImageIdAndSource(jsonObject, out var currentImageId, out var currentSource)
                    && string.Equals(currentImageId, imageId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(currentSource))
                {
                    source = currentSource;
                    return true;
                }

                foreach (var property in jsonObject)
                {
                    if (property.Value != null && TryFindImageSource(property.Value, imageId, out source))
                    {
                        return true;
                    }
                }
            }

            if (node is JsonArray jsonArray)
            {
                foreach (var child in jsonArray)
                {
                    if (child != null && TryFindImageSource(child, imageId, out source))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetImageIdAndSource(JsonObject jsonObject, out string imageId, out string source)
        {
            imageId = string.Empty;
            source = string.Empty;

            if (!TryGetString(jsonObject, "imageId", out imageId))
            {
                return false;
            }

            TryGetString(jsonObject, "source", out source);
            return true;
        }

        private static bool TryGetString(IReadOnlyDictionary<string, JsonElement> properties, string propertyName, out string value)
        {
            value = string.Empty;
            if (!properties.TryGetValue(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = element.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetString(JsonObject jsonObject, string propertyName, out string value)
        {
            value = string.Empty;
            if (!jsonObject.TryGetPropertyValue(propertyName, out var node) || node == null)
            {
                return false;
            }

            try
            {
                value = node.GetValue<string>() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
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
                if (rowData.Height.HasValue && rowData.Height.Value > 0)
                {
                    return rowData.Height.Value;
                }

                if (rowData.ActualHeight.HasValue && rowData.ActualHeight.Value > 0)
                {
                    return rowData.ActualHeight.Value;
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

        private static bool TryDecodeImageBytes(string? source, out byte[] imageBytes)
        {
            imageBytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var payload = ExtractBase64Payload(source);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            try
            {
                imageBytes = Convert.FromBase64String(payload);
                if (imageBytes.Length == 0)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string? ExtractBase64Payload(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            var payload = data.Trim();
            var commaIndex = payload.IndexOf(',');
            if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
            {
                payload = payload[(commaIndex + 1)..];
            }

            if (payload.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
            {
                payload = payload["base64:".Length..];
            }

            return payload;
        }

        private static bool TryConvertToMigraDocImageSource(string? source, out string imageSource)
        {
            imageSource = string.Empty;
            if (!TryDecodeImageBytes(source, out var imageBytes))
            {
                return false;
            }

            imageSource = $"base64:{Convert.ToBase64String(imageBytes)}";
            return true;
        }

        private static bool LooksLikeInlineImagePayload(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var payload = source.Trim();
            return payload.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                || payload.StartsWith("base64:", StringComparison.OrdinalIgnoreCase);
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
            if (!element.TryGetProperty("source", out _) && !element.TryGetProperty("imageId", out _))
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

        private static string? TryGetStringProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return property.GetString();
        }

        private static double? TryGetDoubleProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numberValue))
            {
                return numberValue;
            }

            return null;
        }

    }
}
