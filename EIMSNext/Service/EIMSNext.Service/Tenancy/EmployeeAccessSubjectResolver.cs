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

        var departmentIds = resolver.GetRepository<EmployeeDepartment>().Queryable
            .Where(x => x.CorpId == context.CorpId && x.EmployeeId == employee.Id && !x.DeleteFlag)
            .Select(x => x.DepartmentId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hierarchyIds = resolver.GetRepository<Department>().Queryable
            .Where(x => x.CorpId == context.CorpId && departmentIds.Contains(x.Id) && !x.DeleteFlag)
            .Select(x => x.HeriarchyId)
            .ToList();

        var ancestorDepartmentIds = hierarchyIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => x.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _current = new EmployeeAccessSubjects(
            employee.Id,
            departmentIds,
            ancestorDepartmentIds,
            employee.EmployeeGroups
                .Select(x => x.EmployeeGroupId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
