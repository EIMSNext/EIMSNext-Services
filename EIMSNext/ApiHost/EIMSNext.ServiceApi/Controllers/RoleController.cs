using Asp.Versioning;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Common;
using EIMSNext.Entity;
using EIMSNext.ServiceApi.Authorization;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.ServiceApi.Controllers
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
        public virtual async Task<ActionResult> AddEmps(AddEmpsToRoleRequest request)
        {
            await ApiService.AddEmployeesToRole(request);

            return Ok(ApiResult.Success());
        }

        [HttpPost("RemoveEmps")]
        [Permission(Operation = Operation.Write)]
        public virtual async Task<ActionResult> RemoveEmps(RemoveEmpsToRoleRequest request)
        {
            await ApiService.RemoveEmployeesFromRole(request);

            return Ok(ApiResult.Success());
        }
    }
}
