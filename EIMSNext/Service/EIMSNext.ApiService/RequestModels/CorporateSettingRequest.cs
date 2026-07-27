using EIMSNext.ApiService.RequestModels;

namespace EIMSNext.ApiService.RequestModels;

public sealed class CorporateSettingRequest : RequestBase
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Desc { get; set; } = string.Empty;
}
