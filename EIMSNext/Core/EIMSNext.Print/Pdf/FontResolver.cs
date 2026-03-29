using System.Text.RegularExpressions;
using PdfSharp.Fonts;
using SkiaSharp;

namespace EIMSNext.Print.Pdf
{
    public class FallbackFontResolver : IFontResolver
    {
        public static string DefaultFontName = "fangsong";
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            return new FontResolverInfo(DefaultFontName, isBold, isItalic);
        }

        public byte[]? GetFont(string faceName)
        {
            var parts = faceName.Split('|');
            if (parts.Length != 3) return null;

            string family = parts[0];
            bool.TryParse(parts[1], out var bold);
            bool.TryParse(parts[2], out var italic);

            var font = FontsCache.GetFont(family, bold, italic);
            return font?.FontData;
        }
    }
    public class FontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var font = FontsCache.GetFont(familyName, isBold, isItalic);
            if (font == null) return null;

            return new FontResolverInfo($"{familyName}|{isBold}|{isItalic}".ToLower());
        }

        public byte[]? GetFont(string faceName)
        {
            var parts = faceName.Split('|');
            if (parts.Length != 3) return null;

            string family = parts[0];
            bool.TryParse(parts[1], out var bold);
            bool.TryParse(parts[2], out var italic);

            var font = FontsCache.GetFont(family, bold, italic);
            return font?.FontData;
        }
    }

    internal class FontCacheItem
    {
        public string FamilyName { get; set; } = string.Empty;
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public byte[] FontData { get; set; } = Array.Empty<byte>();
    }
    internal static class FontsCache
    {
        private static bool _initialized = false;
        private static readonly Dictionary<string, FontCacheItem> _fontCache = new();

        /// <summary>
        /// 加载 fonts 目录所有字体到内存缓存
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            lock ("FontsCache_Initialize")
            {
                if (_initialized) return;

                var fontsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts");
                if (!Directory.Exists(fontsPath)) return;

                var files = Directory.EnumerateFiles(fontsPath, "*.*")
                    .Where(f => f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    try
                    {
                        using var typeface = SKTypeface.FromFile(file);
                        if (typeface == null) continue;

                        byte[] fileBytes = File.ReadAllBytes(file);
                        var familyName = RemoveWhiteSpace(typeface.FamilyName);
                        string key = $"{familyName}|{typeface.IsBold}|{typeface.IsItalic}".ToLower();

                        _fontCache[key] = new FontCacheItem
                        {
                            FamilyName = familyName,
                            IsBold = typeface.IsBold,
                            IsItalic = typeface.IsItalic,
                            FontData = fileBytes
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Load Font Fail: {file}, Exception: {ex}");
                    }
                }

                _initialized = true;
            }
        }

        public static FontCacheItem? GetFont(string familyName, bool isBold, bool isItalic)
        {
            familyName = RemoveWhiteSpace(familyName);
            string key = $"{familyName}|{isBold}|{isItalic}".ToLower();
            //string suffix = isBold && isItalic ? "bi" : isBold ? "bd" : isItalic ? "i" : "";

            //string key = $"{familyName}{suffix}".ToLower();
            if (_fontCache.TryGetValue(key, out var item))
                return item;

            var fallback = _fontCache.Values.FirstOrDefault(x => x.FamilyName == familyName);
            return fallback;
        }

        public static string RemoveWhiteSpace(string text)
        {
            return Regex.Replace(text, @"\s+", "");
        }
    }
}
