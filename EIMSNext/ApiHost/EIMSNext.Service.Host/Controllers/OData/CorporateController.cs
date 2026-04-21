using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Deltas;
using EIMSNext.Service.Host.Requests;
using EIMSNext.Service.Host.Authorization;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class CorporateController(IResolver resolver) : ODataController<CorporateApiService, Corporate, CorporateViewModel, CorporateRequest>(resolver)
    {
        [Permission(Operation = Common.Operation.NotSet)]
        public override Task<ActionResult> Post([FromBody] CorporateRequest model)
        {
            return base.Post(model);
        }

        [IdentityType(IdentityType.CorpOwmer)]
        public override Task<ActionResult> Put([FromODataUri] string key, [FromBody] CorporateRequest model)
        {
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
            return base.Patch(key, delta);
        }

        [IdentityType(IdentityType.CorpOwmer)]
        public override Task<ActionResult> Delete([FromODataUri] string key, [FromBody] DeleteBatch? batch)
        {
            return base.Delete(key, batch);
        }
    }
}
