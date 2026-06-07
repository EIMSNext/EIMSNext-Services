using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
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
	public class EmployeeController(IResolver resolver) : ApiControllerBase<EmployeeApiService, Employee, EmployeeViewModel>(resolver)
	{
	    [HttpPost("ReviewJoinCorporate")]
        [Permission(Operation = Operation.Write)]
        [IdentityType(IdentityType.Corp_Admins)]
        public async Task<ActionResult> ReviewJoinCorporate([FromBody] ReviewJoinCorporateRequest request)
        {
            await ApiService.ReviewJoinCorporateAsync(request.EmployeeIds ?? [], request.Approved);
            return Ok(ApiResult.Success());
        }

        [HttpPost("AcceptInvite")]
        [IdentityType(IdentityType.NoCorp)]
        public async Task<ActionResult> AcceptInvite([FromBody] AcceptEmployeeInviteRequest request)
        {
            var currentUser = ServiceContext.User as EIMSNext.Auth.Entities.User;
            if (currentUser == null)
            {
                return BadRequest("未登录用户");
            }

            await ApiService.AcceptInviteAsync(currentUser.Id, currentUser.Phone, currentUser.Email, request.Accepted);
            return Ok(ApiResult.Success());
        }
	}
}
