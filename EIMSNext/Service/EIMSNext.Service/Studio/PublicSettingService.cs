using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
	public class PublicSettingService(IResolver resolver) : EntityServiceBase<PublicSetting>(resolver), IPublicSettingService
	{
	}
}
