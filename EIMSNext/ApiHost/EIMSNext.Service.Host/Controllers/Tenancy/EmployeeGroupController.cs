using Asp.Versioning;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Entities;
using EIMSNext.Service.Host.Authorization;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class EmployeeGroupController(IResolver resolver) : ApiControllerBase<EmployeeGroupApiService, EmployeeGroup, EmployeeGroupViewModel>(resolver)
    {
        [HttpPost("AddEmps")]
        [Permission(ResourceCode = Resources.EmployeeGroup, Operation = Operation.Add)]
        public virtual async Task<ActionResult> AddEmps([FromBody] AddEmployeesToEmployeeGroupRequest request)
        {
            if (!string.IsNullOrEmpty(request.EmployeeGroupId) && request.EmpIds?.Count > 0)
            {
                await ApiService.AddEmployeesToEmployeeGroup(request);

                return Ok(ApiResult.Success());
            }

            return BadRequest();
        }

        [HttpPost("RemoveEmps")]
        [Permission(ResourceCode = Resources.EmployeeGroup, Operation = Operation.Delete)]
        public virtual async Task<ActionResult> RemoveEmps([FromBody] RemoveEmployeesFromEmployeeGroupRequest request)
        {
            if (!string.IsNullOrEmpty(request.EmployeeGroupId) && request.EmpIds?.Count > 0)
            {
                await ApiService.RemoveEmployeesFromEmployeeGroup(request);

                return Ok(ApiResult.Success());
            }

            return BadRequest();
        }

        [HttpPost("Move")]
        [Permission(ResourceCode = Resources.EmployeeGroup, Operation = Operation.Edit)]
        public virtual async Task<ActionResult> Move([FromBody] MoveEmployeeGroupTreeNodeRequest request)
        {
            try
            {
                var moved = await ApiService.Move(request);
                if (!moved)
                {
                    return string.IsNullOrWhiteSpace(request.Id) ? BadRequest() : NotFound();
                }

                return Ok(ApiResult.Success());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
