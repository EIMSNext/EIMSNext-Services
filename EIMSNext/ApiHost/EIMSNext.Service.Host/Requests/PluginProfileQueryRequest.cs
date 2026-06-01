namespace EIMSNext.Service.Host.Requests;

public class PluginProfileQueryRequest
{
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public string? Scenario { get; set; }
    public bool? Recommended { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 24;
}
