namespace EIMSNext.Plugin.Contracts
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PluginAttribute : Attribute
    {
        public PluginAttribute(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
        public string Version { get; init; } = "1.0";
        public string? Description { get; init; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class PluginFunctionAttribute : Attribute
    {
        public PluginFunctionAttribute(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
        public string? Description { get; init; }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class PluginInputAttribute : Attribute
    {
        public PluginInputAttribute(string name, string fieldType)
        {
            Name = name;
            FieldType = fieldType;
        }

        public string Name { get; }
        public string FieldType { get; }
        public string? Key { get; init; }
        public bool Required { get; init; }
        public bool AllowCustomValue { get; init; } = true;
        public bool AllowFieldMapping { get; init; } = true;
        public string? Description { get; init; }
        public string[] CompatibleFieldTypes { get; init; } = [];
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class PluginSubListAttribute : Attribute
    {
        public PluginSubListAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public string? Key { get; init; }
        public bool Required { get; init; }
        public string? Description { get; init; }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class PluginOutputAttribute : Attribute
    {
        public PluginOutputAttribute(string name, string fieldType)
        {
            Name = name;
            FieldType = fieldType;
        }

        public string Name { get; }
        public string FieldType { get; }
        public string? Key { get; init; }
        public string? Description { get; init; }
    }
}
