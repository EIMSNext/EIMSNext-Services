namespace EIMSNext.Print.Pdf
{
    internal static class PdfTextFontHelper
    {
        public static string ResolveParagraphFontName(string? text, string? currentFontName, bool isBold)
        {
            if (string.IsNullOrEmpty(text))
            {
                return currentFontName ?? "FangSong";
            }

            if (!ContainsChinese(text))
            {
                return currentFontName ?? "FangSong";
            }

            var normalized = FontsCache.RemoveWhiteSpace(currentFontName ?? string.Empty).ToLowerInvariant();
            // For Chinese text, use FangSong when a Latin/unstable YaHei variant would otherwise
            // render with broken metrics or missing glyphs in PDF output.
            if (normalized.Contains("microsoftyaheiui") || normalized.Contains("microsoftyahei"))
            {
                return isBold ? (currentFontName ?? "FangSong") : "FangSong";
            }

            if (normalized.Contains("simsun") ||
                normalized.Contains("nsimsun") ||
                normalized.Contains("simfang") ||
                normalized.Contains("fangsong") ||
                normalized.Contains("simhei") ||
                normalized.Contains("simkai"))
            {
                return currentFontName ?? "FangSong";
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
