namespace EIMSNext.Common;

public class OAuthSettings
{
    public string? BaseUrl { get; set; }
    public string? Authority { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public bool? RequireHttpsMetadata { get; set; }

    public string? TokenEndPoint => BuildEndpoint("connect/token");
    public string? SystemTokenEndPoint => BuildEndpoint("system/token");

    private string? BuildEndpoint(string relativePath)
    {
        var baseUrl = BaseUrl ?? Authority;
        return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl.TrimEnd('/')}/{relativePath}";
    }
}
