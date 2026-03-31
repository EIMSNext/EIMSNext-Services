using Asp.Versioning;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Api.Authorization;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Api.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class RoleController(IResolver resolver) : ApiControllerBase<RoleApiService, Role, RoleViewModel>(resolver)
    {
        [HttpPost("AddEmps")]
        [Permission(Operation = Operation.Write)]
        public virtual async Task<ActionResult> AddEmps([FromBody] AddEmpsToRoleRequest request)
        {
            if (!string.IsNullOrEmpty(request.RoleId) && request.EmpIds?.Count > 0)
            {
                await ApiService.AddEmployeesToRole(request);

                return Ok(ApiResult.Success());
            }

            return BadRequest();
        }

        [HttpPost("RemoveEmps")]
        [Permission(Operation = Operation.Write)]
        public virtual async Task<ActionResult> RemoveEmps([FromBody] RemoveEmpsToRoleRequest request)
        {
            if (!string.IsNullOrEmpty(request.RoleId) && request.EmpIds?.Count > 0)
            {
                await ApiService.RemoveEmployeesFromRole(request);

                return Ok(ApiResult.Success());
            }

            return BadRequest();
        }
    }
}
