namespace EIMSNext.Plugin.Interface
{
    public class PluginSetting
    {
        public string? Settings { get; set; }
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
}
