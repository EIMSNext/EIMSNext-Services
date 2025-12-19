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
    public class RoleController(IResolver resolver) : ApiControllerBase<Role, RoleViewModel>(resolver)
    {
        [HttpPost("addemps")]
        [Permission(Operation = Operation.Write)]
        public virtual async Task<ActionResult> AddEmps(AddEmpsToRoleRequest request)
        {
            await (ApiService as RoleApiService)!.AddEmployeesToRole(request);

            return Ok(ApiResult.Success());
        }
    }
}
