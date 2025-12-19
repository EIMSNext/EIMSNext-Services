using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ServiceApi.OData;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using EIMSNext.Common;
using EIMSNext.Core;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.UriParser;
using EIMSNext.ApiService;
using EIMSNext.ServiceApi.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;

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
        public virtual ActionResult AddEmps(AddEmpsToRoleRequest request)
        {
            
            return Ok(ApiResult.Success());
        }
    }
}
