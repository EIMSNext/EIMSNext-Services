using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class WebhookAliasService(IResolver resolver) : EntityServiceBase<WebhookAlias>(resolver), IWebhookAliasService
    {
    }
}
