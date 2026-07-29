using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 企业插件安装记录。
    /// 每条记录代表一个 corp 对某个插件的安装实例。
    /// </summary>
    public class PluginInstall : CorpEntityBase
    {
        /// <summary>插件标识（与 PluginProfile.PluginId 一致，便于直接检索）。</summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>插件名称（冗余字段，UI 直接展示，避免每次 join Profile）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>插件简介（冗余字段）。</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>插件图标 URL（冗余字段）。</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>安装状态，对应 <see cref="PluginInstallStatus"/> 常量之一。</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>运行时是否启用；与 <see cref="Status"/> 联合控制实际生效。</summary>
        public bool Enabled { get; set; }

        /// <summary>安装时间戳（毫秒）。</summary>
        public long InstalledAt { get; set; }

        /// <summary>执行安装的操作人。</summary>
        public Operator? InstalledBy { get; set; }

        /// <summary>最近一次启用时间戳（毫秒）；未启用过为 null。</summary>
        public long? LastEnabledAt { get; set; }

        /// <summary>最近一次停用时间戳（毫秒）；未停用过为 null。</summary>
        public long? LastDisabledAt { get; set; }

        /// <summary>卸载时间戳（毫秒）；仍在安装状态为 null。</summary>
        public long? UninstalledAt { get; set; }

        /// <summary>插件自定义配置（JSON 字符串）。</summary>
        public string? Settings { get; set; }

        /// <summary>安装来源：<c>market</c>（插件市场）/ <c>local</c>（本地文件）/ <c>dev</c>（开发模式）。</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>租户侧的业务流水号，便于与计费/工单系统对账。</summary>
        public string OrderNo { get; set; } = string.Empty;

        /// <summary>授权到期时间戳（毫秒）；永久授权为 null。</summary>
        public long? ExpireAt { get; set; }
    }

    /// <summary>
    /// 插件安装状态常量。
    /// </summary>
    public static class PluginInstallStatus
    {
        /// <summary>已安装（运行时是否生效还要看 <see cref="PluginInstall.Enabled"/>）。</summary>
        public const string Installed = "Installed";

        /// <summary>已卸载（保留安装记录，但不参与运行时）。</summary>
        public const string Uninstalled = "Uninstalled";
    }
}
