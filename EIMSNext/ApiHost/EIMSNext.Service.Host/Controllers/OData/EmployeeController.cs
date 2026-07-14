using Asp.Versioning;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class EmployeeController(IResolver resolver) : ODataController<EmployeeApiService, Employee, EmployeeViewModel, EmployeeRequest>(resolver)
    {
        [HttpGet]
        [Permission(
            ResourceCode = Resources.Employee,
            Operation = Operation.Read,
            AccessControlLevel = AccessControlLevel.Allow)]
        public override IActionResult Get(ODataQueryOptions<EmployeeViewModel> options)
        {
            return base.Get(options);
        }

        [HttpGet]
        [Permission(
            ResourceCode = Resources.Employee,
            Operation = Operation.Read,
            AccessControlLevel = AccessControlLevel.Allow)]
        public override Microsoft.AspNetCore.OData.Results.SingleResult Get([FromODataUri] string key, ODataQueryOptions<EmployeeViewModel> options)
        {
            return base.Get(key, options);
        }

        [HttpPost]
        [Permission(ResourceCode = Resources.Employee, Operation = Operation.Add)]
        public override async Task<ActionResult> Post([FromBody] EmployeeRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }

            var entity = model.CastTo<EmployeeRequest, Employee>();
            if (!ValidateData(entity, null, out ApiResult? fail))
            {
                return BadRequest(fail?.Message);
            }

            var service = (EmployeeApiService)ApiService;
            await service.AddAsync(entity, model.Departments);

            var result = entity.CastTo<Employee, EmployeeViewModel>();
            return Ok(result);
        }

        [HttpPut]
        [Permission(ResourceCode = Resources.Employee, Operation = Operation.Edit)]
        public override async Task<ActionResult> Put([FromODataUri] string key, [FromBody] EmployeeRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }

            if (key != model.Id)
            {
                return BadRequest("请求修改对象的Key不一致");
            }

            Employee? entity = await ApiService.GetAsync(key);
            if (entity == null)
            {
                return NotFound();
            }

            model.CopyTo(entity);
            var service = (EmployeeApiService)ApiService;
            await service.ReplaceAsync(entity, model.Departments, syncDepartments: true);

            var result = entity.CastTo<Employee, EmployeeViewModel>();
            return Ok(result);
        }

        [HttpPatch]
        [Permission(ResourceCode = Resources.Employee, Operation = Operation.Edit)]
        public override async Task<ActionResult> Patch([FromODataUri] string key, [FromBody] Delta<EmployeeRequest> delta)
        {
            if (delta == null)
            {
                return BadRequest("数据解析失败，请检查数据格式，确认正确的字段名和数据类型");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }
            else if (TryGetId(delta, out string sId) && key != sId)
            {
                return BadRequest("请求修改对象的Key不一致");
            }

            Employee? entity = await ApiService.GetAsync(key);
            if (entity == null)
            {
                return NotFound();
            }

            ServiceContext.ScopeCache.Set<Employee>(entity.Id, entity.DeepClone());

            var model = entity.CastTo<Employee, EmployeeRequest>();
            var syncDepartments = delta.GetChangedPropertyNames().Any(x => x.Equals(nameof(EmployeeRequest.Departments), StringComparison.OrdinalIgnoreCase));
            delta.Patch(model);
            model.CopyTo(entity);

            if (!ValidateData(entity, delta, out ApiResult? fail))
            {
                return BadRequest(fail?.Message);
            }

            var service = (EmployeeApiService)ApiService;
            await service.ReplaceAsync(entity, model.Departments, syncDepartments);

            var result = entity.CastTo<Employee, EmployeeViewModel>();
            return Ok(result);
        }

        protected override IQueryable<EmployeeViewModel> FilterResult(IQueryable<EmployeeViewModel> query, ODataQueryOptions<EmployeeViewModel> options)
        {
            query = base.FilterResult(query, options);
            query = query.Where(x => !x.IsDummy);

            if (IsAdminScope())
            {
                var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
                if (evaluator.ShouldApplyNormalAdminRules)
                {
                    var snapshot = evaluator.GetSnapshot();
                    if (snapshot.ContactViewDepartmentScopeMode != AdminPermissionSnapshot.ToWireScopeMode(ScopeMode.All))
                    {
                        var ids = snapshot.ContactViewDepartmentIds;
                        if (ids.Count == 0)
                        {
                            query = query.Where(x => false);
                        }
                        else
                        {
                            var empIds = Resolver.GetRepository<EmployeeDepartment>().Queryable
                                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && ids.Contains(x.DepartmentId))
                                .Select(x => x.EmployeeId)
                                .Distinct()
                                .ToList();
                            query = query.Where(x => empIds.Contains(x.Id));
                        }
                    }
                }
            }

            return query;
        }

        private bool IsAdminScope()
        {
            return Request.Query.TryGetValue("adminScope", out var value) &&
                string.Equals(value.FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
