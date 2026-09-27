using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Entities;
using EIMSNext.Service.Host.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.CorpAdmin)]
	public class TenantAdminGroupController(IResolver resolver) : ApiControllerBase<TenantAdminGroupApiService, TenantAdminGroup, TenantAdminGroupViewModel>(resolver)
	{
        [HttpPost("Move")]
        [Permission(Operation = Operation.Edit)]
        public async Task<ActionResult<TenantAdminGroup>> Move([FromBody] MoveTenantAdminGroupRequest request)
        {
            try
            {
                var group = await ApiService.Move(request);
                if (group == null)
                {
                    return string.IsNullOrWhiteSpace(request.Id) ? BadRequest() : NotFound();
                }

                return Ok(group);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
	}
}
