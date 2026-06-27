using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 客户端授权的 OData CRUD 控制器。
    /// 实体集：<c>ClientGrant</c>，仅 CorpAdmin 可访问（由 <c>IdentityTypeFilter</c> 默认设置保证）。
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class ClientGrantController(IResolver resolver)
        : ODataController<ClientGrantApiService, ClientGrant, ClientGrantViewModel, ClientGrantRequest>(resolver)
    {
    }
}
