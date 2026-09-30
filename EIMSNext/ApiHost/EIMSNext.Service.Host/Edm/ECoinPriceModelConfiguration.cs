using EIMSNext.ApiService.ViewModels;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// E 币定价的 OData 模型注册。
    /// 实体集名：<c>ECoinPrice</c>（由 <see cref="Base.ModelConfigurationBase{T}"/> 从 ViewModel 名截取），
    /// 与 <c>OData/ECoinPriceController</c> 的只读实体集对应。
    /// </summary>
    public class ECoinPriceModelConfiguration : ModelConfigurationBase<ECoinPriceViewModel>
    {
    }
}
