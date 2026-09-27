using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;

namespace EIMSNext.Service;

public sealed class EmployeeAccessSubjectResolver(IResolver resolver) : IEmployeeAccessSubjectResolver
{
    private EmployeeAccessSubjects? _current;

    public EmployeeAccessSubjects ResolveCurrent()
    {
        if (_current != null)
        {
            return _current;
        }

        var context = resolver.GetServiceContext();
        var employee = context.Employee as Employee;
        if (employee == null || string.IsNullOrWhiteSpace(context.CorpId))
        {
            return _current = EmployeeAccessSubjects.Empty;
        }

        var relations = resolver.GetRepository<EmployeeDepartment>().Queryable
            .Where(x => x.CorpId == context.CorpId && x.EmployeeId == employee.Id && !x.DeleteFlag)
            .Select(x => new { x.DepartmentId, x.HeriarchyId })
            .ToList();

        var departmentIds = relations
            .Select(x => x.DepartmentId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 祖先部门直接从关系表的层级路径快照展开，省去对 Department 表的第二次查询。
        var ancestorDepartmentIds = relations
            .Where(x => !string.IsNullOrWhiteSpace(x.HeriarchyId))
            .SelectMany(x => x.HeriarchyId.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 员工组归属由关系表 EmployeeGroupMember 承载。
        var employeeGroupIds = resolver.GetRepository<EmployeeGroupMember>().Queryable
            .Where(x => x.CorpId == context.CorpId && x.EmployeeId == employee.Id && !x.DeleteFlag)
            .Select(x => x.EmployeeGroupId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _current = new EmployeeAccessSubjects(
            employee.Id,
            departmentIds,
            ancestorDepartmentIds,
            employeeGroupIds);
    }
}
