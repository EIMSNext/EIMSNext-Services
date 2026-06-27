using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.OData;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// OAuth 客户端的 OData CRUD 控制器。
    ///
    /// 实体集：<c>Client</c>。仅 <c>IdentityTypeDefaults.CorpAdmin</c> 身份可访问。
    /// <c>ClientSecrets</c> 在 EDM 中被 <c>Ignore()</c>，永远不出现在 OData 响应/请求中；
    /// 改密需走 <c>ClientController.GenerateSecret</c> 端点。
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.CorpAdmin)]
    public class ClientController(IResolver resolver)
        : ODataController<ClientApiService, Client, ClientViewModel, ClientRequest>(resolver)
    {
    }
}
