using HKH.Mef2.Integration;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

namespace EIMSNext.Service
{
	public class FormNotificationService(IResolver resolver) : EntityServiceBase<FormNotification>(resolver), IFormNotificationService
	{
	}
}
