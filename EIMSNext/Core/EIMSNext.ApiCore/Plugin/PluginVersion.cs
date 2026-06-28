using System.Globalization;

namespace EIMSNext.ApiCore.Plugin
{
    /// <summary>
    /// 插件版本号解析。
    /// <para>
    /// 优先按 <see cref="System.Version"/> 解析（如 <c>1.0</c>、<c>1.2.3.4</c>），失败时按 SemVer 解析
    /// （如 <c>1.0.0-rc.1</c>、<c>2.0.0-beta.2</c>），并对两者做归一化以支持比较。
    /// </para>
    /// <para>
    /// 比较规则：先比主版本号（major.minor.patch.patch4），再按 SemVer 规则比较预发布标签
    /// （带预发布标签的版本被认为低于不带标签的同等版本号）。
    /// </para>
    /// </summary>
    public readonly struct PluginVersion : IComparable<PluginVersion>, IEquatable<PluginVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public int Revision { get; }
        public string? PreRelease { get; }
        public string OriginalText { get; }

        private PluginVersion(int major, int minor, int patch, int revision, string? preRelease, string originalText)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Revision = revision;
            PreRelease = preRelease;
            OriginalText = originalText;
        }

        public static bool TryParse(string? input, out PluginVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var text = input.Trim();

            // 拆分预发布标签
            string? preRelease = null;
            var dashIndex = text.IndexOf('-');
            if (dashIndex > 0 && dashIndex < text.Length - 1)
            {
                preRelease = text[(dashIndex + 1)..];
                text = text[..dashIndex];
            }

            if (System.Version.TryParse(text, out var sysVer))
            {
                version = new PluginVersion(
                    sysVer.Major,
                    sysVer.Minor < 0 ? 0 : sysVer.Minor,
                    sysVer.Build < 0 ? 0 : sysVer.Build,
                    sysVer.Revision < 0 ? 0 : sysVer.Revision,
                    preRelease,
                    input);
                return true;
            }

            // SemVer 兜底：major.minor.patch 三段必填
            var parts = text.Split('.');
            if (parts.Length < 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var patch))
            {
                return false;
            }

            version = new PluginVersion(major, minor, patch, 0, preRelease, input);
            return true;
        }

        public int CompareTo(PluginVersion other)
        {
            var cmp = Major.CompareTo(other.Major);
            if (cmp != 0) return cmp;

            cmp = Minor.CompareTo(other.Minor);
            if (cmp != 0) return cmp;

            cmp = Patch.CompareTo(other.Patch);
            if (cmp != 0) return cmp;

            cmp = Revision.CompareTo(other.Revision);
            if (cmp != 0) return cmp;

            // SemVer: 有预发布标签的版本 < 同号无预发布版本
            var hasPre = !string.IsNullOrEmpty(PreRelease);
            var otherHasPre = !string.IsNullOrEmpty(other.PreRelease);
            if (hasPre && !otherHasPre) return -1;
            if (!hasPre && otherHasPre) return 1;
            if (!hasPre && !otherHasPre) return 0;

            return string.Compare(PreRelease, other.PreRelease, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(PluginVersion other) => CompareTo(other) == 0;

        public override bool Equals(object? obj) => obj is PluginVersion other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Revision, PreRelease);

        public override string ToString() => OriginalText;

        public static bool operator <(PluginVersion left, PluginVersion right) => left.CompareTo(right) < 0;
        public static bool operator >(PluginVersion left, PluginVersion right) => left.CompareTo(right) > 0;
        public static bool operator <=(PluginVersion left, PluginVersion right) => left.CompareTo(right) <= 0;
        public static bool operator >=(PluginVersion left, PluginVersion right) => left.CompareTo(right) >= 0;
        public static bool operator ==(PluginVersion left, PluginVersion right) => left.Equals(right);
        public static bool operator !=(PluginVersion left, PluginVersion right) => !left.Equals(right);
    }
}
