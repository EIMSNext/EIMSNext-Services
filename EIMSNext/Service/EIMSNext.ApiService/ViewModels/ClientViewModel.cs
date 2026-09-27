using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// <see cref="Client"/> 的 OData 视图模型。
    ///
    /// 注意：<c>ClientSecrets</c> 由 <c>ClientModelConfiguration</c> 上的 <c>Ignore()</c>
    /// 从 OData 响应中完全排除（包括 POST/PATCH 输入），因此本类不展示该字段。
    /// </summary>
    public class ClientViewModel : Client
    {
    }
}
