using Asp.Versioning;

using HKH.Mef2.Integration;

using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;

using Microsoft.AspNetCore.OData.Query;
using EIMSNext.ApiService;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
        public class WfTaskController(IResolver resolver) : ODataController<WfTaskApiService, Wf_Task, WfTaskRequest>(resolver)
    {
        protected override IQueryable<Wf_Task> FilterResult(IQueryable<Wf_Task> query, ODataQueryOptions<Wf_Task> options)
        {
            return FilterByPermission(query, options);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected override IQueryable<Wf_Task> FilterByPermission(IQueryable<Wf_Task> query, ODataQueryOptions<Wf_Task> options)
        {
            if (IdentityContext.CurrentEmployee != null)
            {
                var empId = IdentityContext.CurrentEmployee.Id;
                return query.Where(x => x.EmployeeId == empId);
            }

            return query;
        }
    }
}

