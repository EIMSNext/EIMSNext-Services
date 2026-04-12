namespace EIMSNext.Print.Pdf
{
    internal static class PdfTextFontHelper
    {
        public static string ResolveParagraphFontName(string? text, string? currentFontName, bool isBold)
        {
            string safeFontName = string.IsNullOrEmpty(currentFontName) ? "FangSong" : currentFontName;

            if (string.IsNullOrEmpty(text) || !ContainsChinese(text))
            {
                return safeFontName;
            }

            var normalized = FontsCache.RemoveWhiteSpace(currentFontName ?? string.Empty).ToLowerInvariant();
            // For Chinese text, use FangSong when a Latin/unstable YaHei variant would otherwise
            // render with broken metrics or missing glyphs in PDF output.
            if (normalized.Contains("microsoftyaheiui") || normalized.Contains("microsoftyahei"))
            {
                return isBold ? safeFontName : "FangSong";
            }

            if (normalized.Contains("simsun") ||
                normalized.Contains("nsimsun") ||
                normalized.Contains("simfang") ||
                normalized.Contains("fangsong") ||
                normalized.Contains("simhei") ||
                normalized.Contains("simkai"))
            {
                return safeFontName;
            }

            return "FangSong";
        }

        public static bool ContainsChinese(string text)
        {
            foreach (var ch in text)
            {
                if (ch >= 0x4E00 && ch <= 0x9FFF)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
