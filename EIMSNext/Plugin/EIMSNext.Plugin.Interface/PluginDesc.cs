namespace EIMSNext.Plugin.Interface
{
    public class PluginDesc
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public IList<FunctionDesc> Functions { get; } = new List<FunctionDesc>();
    }

    public class FunctionDesc
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
    }

    public class ParameterDesc
    {

    }
    public class FieldDesc
    {

    }
}
