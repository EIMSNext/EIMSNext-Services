using Asp.Versioning;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Service.Host.Authorization;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Service.Host.Controllers;

/// <summary>
/// 表单员工/部门组件的数据源接口。与管理 OData 接口隔离。
/// </summary>
[ApiVersion(1.0)]
[IdentityType(IdentityTypeDefaults.BusinessUser)]
public sealed class FormSourceController(IResolver resolver) : EIMSNext.ApiHost.Controllers.MefControllerBase(resolver)
{
    private const int MaxEmployeeTake = 100;
    private const int MaxDepartmentTake = 1000;
    private const string EmployeeSource = "employee";
    private const string DepartmentSource = "department";

    [HttpPost("employees")]
    public async Task<ActionResult> Employees([FromBody] FormMemberSourceRequest request, CancellationToken cancellationToken)
    {
        if (!TryValidateRequest(request, EmployeeSource, out var error))
        {
            return BadRequest(error);
        }
        if (request.Design && !CanUseDesignSource(request))
        {
            return Forbid();
        }

        var source = await LoadSourceAsync(request, cancellationToken, FieldType.Employee1, FieldType.Employee2);
        if (source == null)
        {
            return BadRequest("表单字段不存在或不是员工组件");
        }

        var repository = Resolver.GetRepository<Employee>();
        var query = repository.Queryable
            .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && !x.IsDummy);

        query = await ApplyEmployeeSourceAsync(
            query,
            source,
            request.Design,
            IsDesignRequestWithoutFormContext(request),
            cancellationToken);
        query = ApplyEmployeeRequestFilter(query, request);
        query = ApplyEmployeeSort(query, request.Sort);

        var take = NormalizeTake(request.Take, MaxEmployeeTake);
        var result = await query
            .Skip(NormalizeSkip(request.Skip))
            .Take(take + 1)
            .Select(x => new FormMemberSourceViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Label = x.EmpName,
                Status = x.Status,
                Type = EmployeeSource,
            })
            .ToListAsync(cancellationToken);

        var hasMore = result.Count > take;
        return Ok(new { value = result.Take(take).ToList(), hasMore });
    }

    [HttpPost("departments")]
    public async Task<ActionResult> Departments([FromBody] FormMemberSourceRequest request, CancellationToken cancellationToken)
    {
        if (!TryValidateRequest(request, DepartmentSource, out var error))
        {
            return BadRequest(error);
        }
        if (request.Design && !CanUseDesignSource(request))
        {
            return Forbid();
        }

        // 员工组件的选择器也要展示部门树（用于按部门筛选员工），因此员工字段同样允许查询部门。
        var source = await LoadSourceAsync(request, cancellationToken, FieldType.Department1, FieldType.Department2, FieldType.Employee1, FieldType.Employee2);
        if (source == null)
        {
            return BadRequest("表单字段不存在或不是部门组件");
        }

        var repository = Resolver.GetRepository<Department>();
        var query = repository.Queryable
            .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag);

        var (scope, scopeRestricted) = await ApplyDepartmentSourceAsync(
            query,
            source,
            request.Design,
            IsDesignRequestWithoutFormContext(request),
            cancellationToken);

        // 自定义范围的第一级就是范围内各部门本身（父部门不在范围内的那些），
        // 展开节点时再按 ParentId 取子级，子级能否选中仍由范围决定。
        var rootLevel = scopeRestricted && HasRootParentCondition(request.Filter);
        // 注意：rootLevel 时 RemoveRootParentConditions 可能返回 null（整条 filter 就是根条件），
        // 此时不能再回退到 request.Filter，否则根条件会把第一级过滤成空。
        query = ApplyDepartmentRequestFilter(
            scope,
            request,
            rootLevel ? RemoveRootParentConditions(request.Filter) : request.Filter);
        if (rootLevel)
        {
            query = query.Where(department => !scope.Any(parent => parent.Id == department.ParentId));
        }
        query = ApplyDepartmentSort(query, request.Sort);

        var take = NormalizeTake(request.Take, MaxDepartmentTake);
        var result = await query
            .Skip(NormalizeSkip(request.Skip))
            .Take(take + 1)
            .Select(x => new FormMemberSourceViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Label = x.Name,
                Type = DepartmentSource,
                ParentId = x.ParentId,
                HeriarchyId = x.HeriarchyId,
            })
            .ToListAsync(cancellationToken);

        var hasMore = result.Count > take;
        return Ok(new { value = result.Take(take).ToList(), hasMore });
    }

    private async Task<MemberSource?> LoadSourceAsync(FormMemberSourceRequest request, CancellationToken cancellationToken, params string[] fieldTypes)
    {
        if (string.IsNullOrWhiteSpace(request.FormId) || string.IsNullOrWhiteSpace(request.FieldId))
        {
            // The designer can open the picker before a field has been assigned an ID.
            // The query is restricted to the current administrator's directory scope
            // unless the identity is unrestricted; see Apply*SourceAsync.
            return request.Design ? new MemberSource { Mode = MemberSourceMode.All } : null;
        }

        var form = await Resolver.GetRepository<FormDef>().Queryable.FirstOrDefaultAsync(x =>
            x.CorpId == IdentityContext.CurrentCorpId &&
            !x.DeleteFlag &&
            x.Id == request.FormId, cancellationToken);
        if (form == null)
        {
            return null;
        }

        var field = FindField(form.Content.Items, request.FieldId!);
        if (field == null || !fieldTypes.Contains(field.Type, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        if (request.Design)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(form.AppId);
            return new MemberSource { Mode = MemberSourceMode.All };
        }

        EnsureCanReadForm(form);
        return field.Props.MemberSource ?? new MemberSource { Mode = MemberSourceMode.All };
    }

    private bool CanUseDesignSource(FormMemberSourceRequest request)
    {
        if (!request.Design)
        {
            return true;
        }

        var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
        return evaluator.HasUnrestrictedManagementIdentity ||
            evaluator.ShouldApplyNormalAdminRules ||
            IdentityContext.IdentityType == IdentityType.FormAdmin;
    }

    private static bool IsDesignRequestWithoutFormContext(FormMemberSourceRequest request) =>
        request.Design &&
        (string.IsNullOrWhiteSpace(request.FormId) || string.IsNullOrWhiteSpace(request.FieldId));

    private void EnsureCanReadForm(FormDef form)
    {
        var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
        if (evaluator.HasUnrestrictedManagementIdentity)
        {
            return;
        }

        var accessibleFormIds = WorkbenchTargetResolver.GetAccessibleFormIds(
            Resolver,
            IdentityContext,
            form.AppId);
        if (!accessibleFormIds.Contains(form.Id, StringComparer.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("没有访问该表单的权限");
        }
    }

    private static FieldDef? FindField(IEnumerable<FieldDef>? fields, string fieldId, string? parentId = null)
    {
        foreach (var field in fields ?? [])
        {
            var currentId = string.IsNullOrWhiteSpace(parentId) ? field.Field : $"{parentId}>{field.Field}";
            if (currentId.Equals(fieldId, StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }

            var nested = FindField(field.Columns, fieldId, currentId);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private async Task<IQueryable<Employee>> ApplyEmployeeSourceAsync(
        IQueryable<Employee> query,
        MemberSource source,
        bool design,
        bool designWithoutFormContext,
        CancellationToken cancellationToken)
    {
        if (design && !designWithoutFormContext ||
            !design && string.Equals(source.Mode, MemberSourceMode.All, StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        if (designWithoutFormContext)
        {
            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            return evaluator.HasUnrestrictedManagementIdentity
                ? query
                : evaluator.FilterEmployeesForAdminScope(query);
        }

        var items = source.Items ?? [];
        if (items.Count == 0)
        {
            return query.Where(_ => false);
        }

        var corpId = IdentityContext.CurrentCorpId;
        var currentEmployeeId = IdentityContext.CurrentEmployee?.Id;
        var employeeIds = items.Where(x => x.Type == "employee").Select(x => x.Id).Distinct().ToArray();
        var employeeGroupIds = items.Where(x => x.Type == "employeeGroup").Select(x => x.Id).Distinct().ToArray();
        var exactDepartmentIds = items.Where(x => x.Type == "department" && !x.Cascaded).Select(x => x.Id).Distinct().ToArray();
        var cascadedDepartmentIds = items.Where(x => x.Type == "department" && x.Cascaded).Select(x => x.Id).Distinct().ToArray();
        var includeCurrentUser = items.Any(x => x.Type == "dynamic" && x.Id == "curuser");
        var includeCurrentDepartment = items.Any(x => x.Type == "dynamic" && x.Id == "curdept");
        var currentDepartmentId = includeCurrentDepartment
            ? await GetCurrentDepartmentIdAsync(currentEmployeeId, corpId, cancellationToken)
            : null;

        return query.Where(employee =>
            employeeIds.Contains(employee.Id) ||
            employeeGroupIds.Length > 0 && employee.Groups.Any(group =>
                group.CorpId == corpId && !group.DeleteFlag && employeeGroupIds.Contains(group.EmployeeGroupId)) ||
            (exactDepartmentIds.Length > 0 || cascadedDepartmentIds.Length > 0) && employee.Departments.Any(department =>
                department.CorpId == corpId && !department.DeleteFlag &&
                (exactDepartmentIds.Contains(department.DepartmentId) ||
                 cascadedDepartmentIds.Any(scopeId => department.HeriarchyId.Contains($"|{scopeId}|")))) ||
            (includeCurrentUser && currentEmployeeId != null && employee.Id == currentEmployeeId) ||
            (includeCurrentDepartment && currentDepartmentId != null && employee.Departments.Any(department =>
                department.CorpId == corpId && !department.DeleteFlag && department.DepartmentId == currentDepartmentId)));
    }

    private async Task<(IQueryable<Department> Query, bool Restricted)> ApplyDepartmentSourceAsync(
        IQueryable<Department> query,
        MemberSource source,
        bool design,
        bool designWithoutFormContext,
        CancellationToken cancellationToken)
    {
        if (design && !designWithoutFormContext ||
            !design && string.Equals(source.Mode, MemberSourceMode.All, StringComparison.OrdinalIgnoreCase))
        {
            return (query, false);
        }

        if (designWithoutFormContext)
        {
            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            return evaluator.HasUnrestrictedManagementIdentity
                ? (query, false)
                : (evaluator.FilterDepartmentsForAdminScope(query), true);
        }

        var items = source.Items ?? [];
        if (items.Count == 0)
        {
            return (query.Where(_ => false), true);
        }

        var exactDepartmentIds = items.Where(x => x.Type == "department" && !x.Cascaded).Select(x => x.Id).Distinct().ToArray();
        var cascadedDepartmentIds = items.Where(x => x.Type == "department" && x.Cascaded).Select(x => x.Id).Distinct().ToArray();
        var includeCurrentDepartment = items.Any(x => x.Type == "dynamic" && x.Id == "curdept");
        var currentDepartmentId = includeCurrentDepartment
            ? await GetCurrentDepartmentIdAsync(
                IdentityContext.CurrentEmployee?.Id,
                IdentityContext.CurrentCorpId,
                cancellationToken)
            : null;

        return (query.Where(department =>
            exactDepartmentIds.Contains(department.Id) ||
            cascadedDepartmentIds.Any(scopeId => department.HeriarchyId.Contains($"|{scopeId}|")) ||
            (includeCurrentDepartment && currentDepartmentId != null && department.Id == currentDepartmentId)), true);
    }

    private static bool HasRootParentCondition(DynamicFilter? filter)
    {
        if (filter == null)
        {
            return false;
        }

        if (!filter.IsGroup)
        {
            return IsRootParentCondition(filter);
        }

        return (filter.Items ?? []).Any(HasRootParentCondition);
    }

    private static bool IsRootParentCondition(DynamicFilter? filter)
    {
        if (filter == null || filter.IsGroup)
        {
            return false;
        }

        if (!string.Equals(filter.Field, nameof(Department.ParentId), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(filter.Op, FilterOp.Eq, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = DynamicValueNormalizer.Normalize(filter.Value);
        return value is null || (value is string text && text.Length == 0);
    }

    private static DynamicFilter? RemoveRootParentConditions(DynamicFilter? filter)
    {
        if (filter == null)
        {
            return null;
        }

        if (!filter.IsGroup)
        {
            return IsRootParentCondition(filter) ? null : filter;
        }

        var items = (filter.Items ?? [])
            .Select(RemoveRootParentConditions)
            .OfType<DynamicFilter>()
            .ToList();

        return items.Count == 0 ? null : new DynamicFilter { Rel = filter.Rel, Items = items };
    }

    private IQueryable<Employee> ApplyEmployeeRequestFilter(IQueryable<Employee> query, FormMemberSourceRequest request)
    {
        var filter = ValidateAndNormalizeFilter(request.Filter, new HashSet<string>(["Id", "Code", "EmpName", "Status"], StringComparer.OrdinalIgnoreCase));
        if (filter != null)
        {
            query = query.Where(filter.ToPredicate<Employee>());
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.EmpName.Contains(keyword) || x.Code.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.DepartmentId))
        {
            var departmentId = request.DepartmentId;
            var cascaded = request.DepartmentCascaded;
            var corpId = IdentityContext.CurrentCorpId;
            query = query.Where(x => x.Departments.Any(d =>
                d.CorpId == corpId && !d.DeleteFlag &&
                (d.DepartmentId == departmentId || (cascaded && d.HeriarchyId.Contains($"|{departmentId}|")))));
        }

        return query;
    }

    private IQueryable<Department> ApplyDepartmentRequestFilter(
        IQueryable<Department> query,
        FormMemberSourceRequest request,
        DynamicFilter? filter)
    {
        filter = ValidateAndNormalizeFilter(filter, new HashSet<string>(["Id", "Code", "Name", "ParentId"], StringComparer.OrdinalIgnoreCase));
        if (filter != null)
        {
            query = query.Where(filter.ToPredicate<Department>());
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        }

        return query;
    }

    private IQueryable<Employee> ApplyEmployeeSort(IQueryable<Employee> query, DynamicSortList? sort)
    {
        var safeSort = ValidateSort(sort, new HashSet<string>(["Id", "Code", "EmpName", "Status"], StringComparer.OrdinalIgnoreCase));
        return safeSort == null
            ? query.OrderBy(x => x.EmpName).ThenBy(x => x.Id)
            : query.OrderBy(safeSort.ToSortDefinition<Employee>());
    }

    private IQueryable<Department> ApplyDepartmentSort(IQueryable<Department> query, DynamicSortList? sort)
    {
        var safeSort = ValidateSort(sort, new HashSet<string>(["Id", "Code", "Name", "ParentId"], StringComparer.OrdinalIgnoreCase));
        return safeSort == null
            ? query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            : query.OrderBy(safeSort.ToSortDefinition<Department>());
    }

    private DynamicFilter? ValidateAndNormalizeFilter(DynamicFilter? filter, IReadOnlySet<string> allowedFields)
    {
        if (filter == null)
        {
            return null;
        }

        filter.ClearValueExpressions();
        DynamicFilterValidator.Validate(filter);
        ValidateFilterFields(filter, allowedFields);
        return DynamicFilterRules.Normalize(filter);
    }

    private static void ValidateFilterFields(DynamicFilter filter, IReadOnlySet<string> allowedFields)
    {
        if (filter.IsGroup)
        {
            foreach (var item in filter.Items ?? [])
            {
                ValidateFilterFields(item, allowedFields);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(filter.Field) && !allowedFields.Contains(filter.Field))
        {
            throw new BadRequestException($"不支持的过滤字段: {filter.Field}");
        }
    }

    private static DynamicSortList? ValidateSort(DynamicSortList? sort, IReadOnlySet<string> allowedFields)
    {
        if (sort == null || sort.Count == 0)
        {
            return null;
        }

        foreach (var item in sort)
        {
            if (!allowedFields.Contains(item.Field))
            {
                throw new BadRequestException($"不支持的排序字段: {item.Field}");
            }
        }

        return sort;
    }

    private async Task<string?> GetCurrentDepartmentIdAsync(
        string? employeeId,
        string corpId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return null;
        }

        return await Resolver.GetRepository<EmployeeDepartment>().Queryable
            .Where(x => x.CorpId == corpId && !x.DeleteFlag && x.EmployeeId == employeeId)
            .OrderBy(x => x.SortValue)
            .ThenBy(x => x.Id)
            .Select(x => x.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool TryValidateRequest(FormMemberSourceRequest request, string sourceType, out string error)
    {
        if (!string.Equals(request.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
        {
            error = "数据源类型无效";
            return false;
        }

        if (!request.Design && (string.IsNullOrWhiteSpace(request.FormId) || string.IsNullOrWhiteSpace(request.FieldId)))
        {
            error = "运行时必须提供表单 ID 和字段 ID";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int NormalizeSkip(int skip) => Math.Max(0, skip);

    private static int NormalizeTake(int take, int maxTake) => RequestPagingPolicy.Normalize(take, maxTake);
}
