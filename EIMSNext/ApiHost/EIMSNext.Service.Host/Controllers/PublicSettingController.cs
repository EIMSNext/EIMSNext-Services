using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.BusinessUser)]
	public class PublicSettingController(IResolver resolver) : ApiControllerBase<PublicSettingApiService, PublicSetting, PublicSettingViewModel>(resolver)
	{
        [HttpGet("current")]
        [Permission(Operation = Operation.Read)]
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DashLink | PublicScope.FormLink | PublicScope.DataLink | PublicScope.QueryLink)]
        public ActionResult<PublicSetting?> GetCurrent()
        {
            var targetId = IdentityContext.CurrentDashboardId;
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return NotFound();
            }

            var validator = Resolver.Resolve<IPublicAccessValidator>();
            if (!validator.IsAnySectionEnabled())
            {
                return NotFound();
            }

            var setting = Resolver.Resolve<IPublicSettingService>().All()
                .FirstOrDefault(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.TargetId == targetId);

            return setting == null ? NotFound() : Ok(SanitizeForPublic(setting));
        }

        private static PublicSetting SanitizeForPublic(PublicSetting setting)
        {
            var sanitized = setting.DeepClone();
            sanitized.Dashboard ??= new PublicDashboardSetting();
            sanitized.Form ??= new PublicFormSetting();
            sanitized.Form.FormLink ??= new PublicFormLinkSetting();
            sanitized.Form.DataLink ??= new PublicDataLinkSetting();
            sanitized.Form.QueryLink ??= new PublicQueryLinkSetting();

            sanitized.Dashboard.AccessCodeHash = string.Empty;
            sanitized.Form.FormLink.AccessCodeHash = string.Empty;
            sanitized.Form.DataLink.AccessCodeHash = string.Empty;
            sanitized.Form.QueryLink.AccessCodeHash = string.Empty;
            return sanitized;
        }
	}
}
