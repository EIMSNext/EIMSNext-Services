using System.Threading;

using HKH.Mef2.Integration;

namespace EIMSNext.Plugin.Contracts
{
    public enum PluginValueType
    {
        Custom,
        Field,
        Formula,
        Empty
    }

    public class PluginSetting
    {
        public string PluginId { get; set; } = string.Empty;
        public string FunctionId { get; set; } = string.Empty;
        public string? Settings { get; set; }
        public List<PluginFieldSetting> FieldSettings { get; set; } = new List<PluginFieldSetting>();
        public List<PluginResultFieldSetting> ResultFields { get; set; } = new List<PluginResultFieldSetting>();
    }

    public class PluginFieldSetting
    {
        public string FieldKey { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public PluginValueType ValueType { get; set; }
        public object? Value { get; set; }
        public PluginFieldReference? ValueField { get; set; }
        public List<PluginFieldSetting> SubFieldSettings { get; set; } = new List<PluginFieldSetting>();
    }

    public class PluginFieldReference
    {
        public string NodeId { get; set; } = string.Empty;
        public string FormId { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public bool IsSubField { get; set; }
        public bool? SingleResultNode { get; set; }
    }

    public class PluginResultFieldSetting
    {
        public string FieldKey { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public List<PluginResultFieldSetting> SubFields { get; set; } = new List<PluginResultFieldSetting>();
    }

    public class PluginExecArgs
    {
        public required string FunName { get; set; }
        public string? FunArgs { get; set; }
    }

    /// <summary>
    /// 插件执行结果。
    /// </summary>
    /// <remarks>
    /// 错误码约定：
    /// <list type="bullet">
    /// <item><description><c>0</c>：成功；</description></item>
    /// <item><description><c>-1</c>：函数不存在（<c>FindFunction</c> 未命中，大小写不敏感）；</description></item>
    /// <item><description><c>-2</c>：函数参数数量不为 1；</description></item>
    /// <item><description><c>-3</c>：函数执行异常（含参数绑定失败）；</description></item>
    /// <item><description><c>-4</c>：插件未启用 / 已禁用 / 授权已过期（业务层拦截，流程节点显式失败，不静默继续）；</description></item>
    /// <item><description><c>-408</c>：函数执行超时（<c>Plugin:ExecutionTimeoutSeconds</c>，默认 30s）；</description></item>
    /// <item><description><c>-404</c>：插件运行时未找到（插件未安装或程序集缺失）；</description></item>
    /// <item><description><c>-409</c>：插件正在重新加载 / 卸载中。</description></item>
    /// </list>
    /// </remarks>
    public class PluginExecResult
    {
        public int Code { get; set; }
        public string? Message { get; set; }
        public object? Result { get; set; }
    }

    public class PluginInvocationContext
    {
        public IResolver ? Resolver { get; set; }
        public string? CorpId { get; set; }
        public string? UserId { get; set; }
        public IDictionary<string, object?> Items { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// 本次执行的取消信号。同步插件无法被强制中止，长循环里主动检查它才能真正退出。
        /// </summary>
        public CancellationToken CancellationToken { get; set; }
    }

    public class PluginRuntimeInfo
    {
        public string PluginId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? Description { get; set; }
        public IList<FunctionDesc> Functions { get; set; } = new List<FunctionDesc>();
    }

    public class PluginReloadResult
    {
        public IList<PluginReloadItemResult> Items { get; set; } = new List<PluginReloadItemResult>();
    }

    public class PluginReloadItemResult
    {
        public string PluginId { get; set; } = string.Empty;
        public string? PreviousVersion { get; set; }
        public string? CurrentVersion { get; set; }
        public bool Updated { get; set; }
        public bool UnloadedOldVersion { get; set; }
        public string? Message { get; set; }
    }
}
