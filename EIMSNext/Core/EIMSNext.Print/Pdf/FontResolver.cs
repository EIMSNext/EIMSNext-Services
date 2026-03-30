using System.Text.RegularExpressions;
using PdfSharp.Fonts;
using SkiaSharp;

namespace EIMSNext.Print.Pdf
{
    internal static class PdfFontResolverRuntime
    {
        private static PdfRenderOptions _options = new();

        public static PdfRenderOptions Options => _options;

        public static void Configure(PdfRenderOptions? options)
        {
            _options = options ?? new PdfRenderOptions();
        }

        public static string DefaultFontFamily => _options.DefaultFontFamily;

        public static string NormalizeFamilyName(string? familyName)
        {
            var normalized = FontsCache.RemoveWhiteSpace(familyName ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrEmpty(normalized))
            {
                normalized = FontsCache.RemoveWhiteSpace(DefaultFontFamily).ToLowerInvariant();
            }

            if (_options.FontAliases.TryGetValue(normalized, out var alias))
            {
                return alias;
            }

            return familyName?.Trim() ?? DefaultFontFamily;
        }

        public static string ResolveFontFamily(string? familyName, bool isBold, bool isItalic)
        {
            foreach (var candidate in GetCandidates(familyName))
            {
                if (FontsCache.HasFont(candidate, isBold, isItalic) || FontsCache.HasFamily(candidate))
                {
                    return FontsCache.GetCanonicalFamilyName(candidate) ?? candidate;
                }
            }

            return DefaultFontFamily;
        }

        private static IEnumerable<string> GetCandidates(string? familyName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<string>();

            void AddCandidate(string? candidate)
            {
                if (string.IsNullOrWhiteSpace(candidate)) return;
                if (seen.Add(candidate.Trim()))
                {
                    candidates.Add(candidate.Trim());
                }
            }

            AddCandidate(familyName);

            var normalized = FontsCache.RemoveWhiteSpace(familyName ?? string.Empty).ToLowerInvariant();
            if (_options.FontAliases.TryGetValue(normalized, out var alias))
            {
                AddCandidate(alias);
            }

            AddCandidate(DefaultFontFamily);

            foreach (var fallback in _options.FontFallbackChain)
            {
                AddCandidate(fallback);
            }

            return candidates;
        }
    }

    public class FallbackFontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var resolvedFamily = PdfFontResolverRuntime.ResolveFontFamily(familyName, isBold, isItalic);
            return new FontResolverInfo(CreateFaceName(resolvedFamily, isBold, isItalic));
        }

        public byte[]? GetFont(string faceName)
        {
            var font = ResolveFont(faceName);
            return font?.FontData;
        }

        internal static string CreateFaceName(string familyName, bool isBold, bool isItalic)
        {
            return $"{familyName}|{isBold}|{isItalic}".ToLowerInvariant();
        }

        internal static FontCacheItem? ResolveFont(string faceName)
        {
            var parts = faceName.Split('|');
            if (parts.Length != 3) return null;

            var family = PdfFontResolverRuntime.ResolveFontFamily(parts[0], bool.TryParse(parts[1], out var parsedBold) && parsedBold, bool.TryParse(parts[2], out var parsedItalic) && parsedItalic);
            bool.TryParse(parts[1], out var bold);
            bool.TryParse(parts[2], out var italic);

            return FontsCache.GetFont(family, bold, italic);
        }
    }

    public class FontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var resolvedFamily = PdfFontResolverRuntime.ResolveFontFamily(familyName, isBold, isItalic);
            var font = FontsCache.GetFont(resolvedFamily, isBold, isItalic);
            if (font == null) return null;

            return new FontResolverInfo(FallbackFontResolver.CreateFaceName(font.FamilyName, isBold, isItalic));
        }

        public byte[]? GetFont(string faceName)
        {
            var font = FallbackFontResolver.ResolveFont(faceName);
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
        private static readonly object InitializeLock = new();

        /// <summary>
        /// 加载 fonts 目录所有字体到内存缓存
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            lock (InitializeLock)
            {
                if (_initialized) return;

                var fontsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts");
                if (!Directory.Exists(fontsPath)) return;

                var files = Directory.EnumerateFiles(fontsPath, "*.*")
                    .Where(f =>
                        f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    try
                    {
                        using var typeface = SKTypeface.FromFile(file);
                        if (typeface == null) continue;

                        byte[] fileBytes = File.ReadAllBytes(file);
                        var familyName = typeface.FamilyName?.Trim();
                        if (string.IsNullOrWhiteSpace(familyName)) continue;

                        string key = BuildKey(familyName, typeface.IsBold, typeface.IsItalic);

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
            string key = BuildKey(familyName, isBold, isItalic);
            if (_fontCache.TryGetValue(key, out var item))
                return item;

            var normalizedFamilyName = RemoveWhiteSpace(familyName);
            var fallback = _fontCache.Values.FirstOrDefault(x => string.Equals(RemoveWhiteSpace(x.FamilyName), normalizedFamilyName, StringComparison.OrdinalIgnoreCase));
            return fallback;
        }

        public static bool HasFont(string familyName, bool isBold, bool isItalic)
        {
            return _fontCache.ContainsKey(BuildKey(familyName, isBold, isItalic));
        }

        public static bool HasFamily(string familyName)
        {
            var normalizedFamilyName = RemoveWhiteSpace(familyName);
            return _fontCache.Values.Any(x => string.Equals(RemoveWhiteSpace(x.FamilyName), normalizedFamilyName, StringComparison.OrdinalIgnoreCase));
        }

        public static string? GetCanonicalFamilyName(string familyName)
        {
            var normalizedFamilyName = RemoveWhiteSpace(familyName);
            return _fontCache.Values
                .FirstOrDefault(x => string.Equals(RemoveWhiteSpace(x.FamilyName), normalizedFamilyName, StringComparison.OrdinalIgnoreCase))
                ?.FamilyName;
        }

        private static string BuildKey(string familyName, bool isBold, bool isItalic)
        {
            return $"{RemoveWhiteSpace(familyName)}|{isBold}|{isItalic}".ToLowerInvariant();
        }

        public static string RemoveWhiteSpace(string text)
        {
            return Regex.Replace(text, @"\s+", "");
        }
    }
}
