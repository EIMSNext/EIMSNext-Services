using HKH.Mef2.Integration;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

namespace EIMSNext.Service
{
	public class WebhookService(IResolver resolver) : EntityServiceBase<Webhook>(resolver), IWebhookService
	{
	}
}
