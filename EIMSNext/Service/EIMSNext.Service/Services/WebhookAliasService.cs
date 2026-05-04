using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class WebhookAliasService(IResolver resolver) : EntityServiceBase<WebhookAlias>(resolver), IWebhookAliasService
    {
    }
}
