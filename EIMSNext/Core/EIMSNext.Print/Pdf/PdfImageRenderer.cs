using MigraDoc.DocumentObjectModel;

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
            if (worksheet.Images == null || worksheet.Images.Count == 0 || workbook.Resources == null || workbook.Resources.Count == 0)
            {
                return;
            }

            var resources = workbook.Resources
                .Where(resource => !string.IsNullOrWhiteSpace(resource.Name) || !string.IsNullOrWhiteSpace(resource.Data))
                .ToDictionary(GetResourceKey, resource => resource, StringComparer.OrdinalIgnoreCase);

            var renderPlan = _layoutCalculator.CreatePlan(worksheet, section.PageSetup);

            foreach (var image in worksheet.Images)
            {
                if (string.IsNullOrWhiteSpace(image.ResourceId))
                {
                    continue;
                }

                if (!TryResolveImagePath(image.ResourceId, resources, _temporaryFileSession, out var imagePath))
                {
                    continue;
                }

                try
                {
                    var textFrame = section.AddTextFrame();
                    var leftCm = section.PageSetup.LeftMargin.Centimeter + _layoutCalculator.MapHorizontalPixelsToCm(worksheet, image.Position.Left, renderPlan.ScaleFactor);
                    var topCm = section.PageSetup.TopMargin.Centimeter + _layoutCalculator.MapVerticalPixelsToCm(worksheet, image.Position.Top);
                    var widthCm = _layoutCalculator.MapHorizontalPixelsToCm(worksheet, image.Position.Width, renderPlan.ScaleFactor);
                    var heightCm = _layoutCalculator.MapVerticalPixelsToCm(worksheet, image.Position.Height);

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
                    // Ignore invalid image data and keep rendering the document.
                }
            }
        }

        private static string GetResourceKey(UniverResource resource)
        {
            if (!string.IsNullOrWhiteSpace(resource.Name))
            {
                return resource.Name;
            }

            return resource.Data.GetHashCode().ToString();
        }

        private static bool TryResolveImagePath(string resourceId, IReadOnlyDictionary<string, UniverResource> resources, PdfTemporaryFileSession temporaryFileSession, out string imagePath)
        {
            imagePath = string.Empty;
            if (!resources.TryGetValue(resourceId, out var resource))
            {
                resource = resources.Values.FirstOrDefault(x => string.Equals(x.Name, resourceId, StringComparison.OrdinalIgnoreCase));
                if (resource == null)
                {
                    return false;
                }
            }

            if (!IsImageResource(resource))
            {
                return false;
            }

            if (!TryDecodeResourceData(resource.Data, out var imageBytes))
            {
                return false;
            }

            var extension = ResolveImageExtension(resource);
            imagePath = temporaryFileSession.CreateFile(extension, imageBytes);
            return true;
        }

        private static bool IsImageResource(UniverResource resource)
        {
            if (string.IsNullOrWhiteSpace(resource.Type))
            {
                return true;
            }

            return resource.Type.Contains("image", StringComparison.OrdinalIgnoreCase);
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

        private static string ResolveImageExtension(UniverResource resource)
        {
            if (resource.Type.Contains("png", StringComparison.OrdinalIgnoreCase)) return "png";
            if (resource.Type.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || resource.Type.Contains("jpg", StringComparison.OrdinalIgnoreCase)) return "jpg";
            if (resource.Type.Contains("bmp", StringComparison.OrdinalIgnoreCase)) return "bmp";
            if (resource.Type.Contains("gif", StringComparison.OrdinalIgnoreCase)) return "gif";
            if (!string.IsNullOrWhiteSpace(resource.Name))
            {
                var extension = Path.GetExtension(resource.Name);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    return extension.TrimStart('.');
                }
            }

            return "png";
        }
    }
}
