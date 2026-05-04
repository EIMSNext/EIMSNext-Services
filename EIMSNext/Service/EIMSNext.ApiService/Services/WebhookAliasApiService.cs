using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class WebhookAliasApiService(IResolver resolver) : ApiServiceBase<WebhookAlias, WebhookAliasViewModel, IWebhookAliasService>(resolver)
    {
    }
}
