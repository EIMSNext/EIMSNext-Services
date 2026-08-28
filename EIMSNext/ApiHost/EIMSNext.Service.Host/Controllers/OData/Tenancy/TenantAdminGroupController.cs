using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.CorpAdmin)]
	public class TenantAdminGroupController(IResolver resolver) : ODataController<TenantAdminGroupApiService, TenantAdminGroup, TenantAdminGroupViewModel, TenantAdminGroupRequest>(resolver)
	{
        public override async Task<ActionResult> Post([FromBody] TenantAdminGroupRequest model)
        {
            try
            {
                return await base.Post(model);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public override async Task<ActionResult> Put([FromODataUri] string key, [FromBody] TenantAdminGroupRequest model)
        {
            try
            {
                return await base.Put(key, model);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public override async Task<ActionResult> Patch([FromODataUri] string key, [FromBody] Delta<TenantAdminGroupRequest> delta)
        {
            try
            {
                return await base.Patch(key, delta);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public override async Task<ActionResult> Patch([FromBody] DeltaSet<TenantAdminGroupRequest> deltas)
        {
            try
            {
                return await base.Patch(deltas);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public override async Task<ActionResult> Delete([FromODataUri] string key, [FromBody] DeleteBatch? batch)
        {
            try
            {
                return await base.Delete(key, batch);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
