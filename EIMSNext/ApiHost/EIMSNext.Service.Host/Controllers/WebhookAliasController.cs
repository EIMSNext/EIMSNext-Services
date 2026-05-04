using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// Webhook字段别名配置
    /// </summary>
    [ApiVersion(1.0)]
    public class WebhookAliasController(IResolver resolver) : ApiControllerBase<WebhookAliasApiService, WebhookAlias, WebhookAliasViewModel>(resolver)
    {
    }
}
