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
        public string? PluginVersion { get; set; }
        public string FunctionId { get; set; } = string.Empty;
        public string? Settings { get; set; }
        public List<PluginFieldSetting> FieldSettings { get; set; } = new List<PluginFieldSetting>();
    }

    public class PluginFieldSetting
    {
        public string FieldKey { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public PluginValueType ValueType { get; set; }
        public object? Value { get; set; }
        public PluginFieldReference? ValueField { get; set; }
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

    public class PluginExecArgs
    {
        public required string FunName { get; set; }
        public string? FunArgs { get; set; }
    }

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
