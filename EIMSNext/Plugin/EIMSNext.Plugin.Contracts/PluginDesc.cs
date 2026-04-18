namespace EIMSNext.Plugin.Contracts
{
    public class PluginDesc
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string Version { get; set; } = string.Empty;
        public string? Description { get; set; }
        public IList<FunctionDesc> Functions { get; } = new List<FunctionDesc>();
    }

    public class FunctionDesc
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public IList<PluginFieldDesc> InputFields { get; } = new List<PluginFieldDesc>();
    }

    public class PluginFieldDesc
    {
        public required string Key { get; set; }
        public required string Name { get; set; }
        public string FieldType { get; set; } = string.Empty;
        public bool Required { get; set; }
        public bool AllowCustomValue { get; set; } = true;
        public bool AllowFieldMapping { get; set; } = true;
        public string? Description { get; set; }
        public IList<string> CompatibleFieldTypes { get; } = new List<string>();
    }
}
