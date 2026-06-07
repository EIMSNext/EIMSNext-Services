using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 企业插件安装记录。
    /// </summary>
    public class PluginInstall : CorpEntityBase
    {
        public string PluginProfileId { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public long InstalledAt { get; set; }
        public Operator? InstalledBy { get; set; }
        public long? LastEnabledAt { get; set; }
        public long? LastDisabledAt { get; set; }
        public long? UninstalledAt { get; set; }
        public string? Settings { get; set; }
        public string Source { get; set; } = string.Empty;
        public string OrderNo { get; set; } = string.Empty;
        public long? ExpireAt { get; set; }
    }

    public static class PluginInstallStatus
    {
        public const string Installed = "Installed";
        public const string Uninstalled = "Uninstalled";
    }
}
