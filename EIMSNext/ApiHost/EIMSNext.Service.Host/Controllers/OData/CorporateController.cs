using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using EIMSNext.Service.Host.Requests;
using EIMSNext.Service.Host.Authorization;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class CorporateController(IResolver resolver) : ODataController<CorporateApiService, Corporate, CorporateRequest>(resolver)
    {
        [Permission(Operation = Common.Operation.NotSet)]
        [IdentityType(IdentityTypeDefaults.Authenticated)]
        public override Task<ActionResult> Post([FromBody] CorporateRequest model)
        {
            return base.Post(model);
        }

        // 无企业用户（NoCorp）需搜索企业后申请加入，故企业名录对全部已认证身份可读；
        // Corporate 只含 名称/简介/注册来源，不涉及企业内数据。
        [IdentityType(IdentityTypeDefaults.Authenticated)]
        public override IActionResult Get(ODataQueryOptions<Corporate> options)
        {
            return base.Get(options);
        }

        [IdentityType(IdentityTypeDefaults.Authenticated)]
        public override Microsoft.AspNetCore.OData.Results.SingleResult Get([FromODataUri] string key, ODataQueryOptions<Corporate> options)
        {
            return base.Get(key, options);
        }

        [IdentityType(IdentityType.CorpOwmer)]
        public override Task<ActionResult> Put([FromODataUri] string key, [FromBody] CorporateRequest model)
        {
            if (!CanMaintain(key))
            {
                return Task.FromResult<ActionResult>(NotFound());
            }

            return base.Put(key, model);
        }

        [Permission(AccessControlLevel = AccessControlLevel.Forbid)]
        public override Task<ActionResult> Patch([FromBody] DeltaSet<CorporateRequest> deltas)
        {
            return base.Patch(deltas);
        }

        [IdentityType(IdentityType.Corp_Admins)]
        public override Task<ActionResult> Patch([FromODataUri] string key, [FromBody] Delta<CorporateRequest> delta)
        {
            if (!CanMaintain(key))
            {
                return Task.FromResult<ActionResult>(NotFound());
            }

            return base.Patch(key, delta);
        }

        [IdentityType(IdentityType.CorpOwmer)]
        public override Task<ActionResult> Delete([FromODataUri] string key, [FromBody] DeleteBatch? batch)
        {
            if (!CanMaintain(key))
            {
                return Task.FromResult<ActionResult>(NotFound());
            }

            return base.Delete(key, batch);
        }

        /// <summary>
        /// 企业档案只能由所属企业维护。Corporate 未实现 ICorpOwned，基类的企业过滤对它不生效，
        /// 因此必须在此显式校验主键归属，否则任一企业所有者都能改写其他企业。
        /// 平台管理员、System 与 Client 身份没有企业上下文，需要跨企业维护企业记录，予以放行。
        /// </summary>
        private bool CanMaintain(string corpId)
        {
            if (IdentityContext.IdentityType is IdentityType.System or IdentityType.Client or IdentityType.PlatAdmin)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(IdentityContext.CurrentCorpId)
                && string.Equals(corpId, IdentityContext.CurrentCorpId, StringComparison.OrdinalIgnoreCase);
        }
    }
}

