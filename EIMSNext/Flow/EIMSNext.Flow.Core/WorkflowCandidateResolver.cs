using System.Collections;
using System.Dynamic;
using System.Linq.Expressions;
using System.Text.Json;

using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;

using Microsoft.EntityFrameworkCore;
using EIMSNext.Core.Extensions;

namespace EIMSNext.Flow.Core
{
    internal sealed class WorkflowCandidateResolver
    {
        private readonly IRepository<Employee> _employeeRepository;
        private readonly IRepository<EmployeeDepartment> _employeeDepartmentRepository;
        private readonly IRepository<Department> _departmentRepository;
        private readonly IRepository<FormDef> _formDefRepository;
        private readonly IRepository<FormData> _formDataRepository;

        public WorkflowCandidateResolver(
            IRepository<Employee> employeeRepository,
            IRepository<EmployeeDepartment> employeeDepartmentRepository,
            IRepository<Department> departmentRepository,
            IRepository<FormDef> formDefRepository,
            IRepository<FormData> formDataRepository)
        {
            _employeeRepository = employeeRepository;
            _employeeDepartmentRepository = employeeDepartmentRepository;
            _departmentRepository = departmentRepository;
            _formDefRepository = formDefRepository;
            _formDataRepository = formDataRepository;
        }

        public async Task<List<string>> ResolveEmployeeIdsAsync(WfDataContext dataContext, IList<ApprovalCandidate>? candidates)
        {
            var empIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (candidates == null || candidates.Count <= 0)
            {
                return new List<string>();
            }

            var deptIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var employeeGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var managerRequests = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            FormData? formData = null;
            FormDef? formDef = null;

            foreach (var candidate in candidates)
            {
                switch (candidate.CandidateType)
                {
                    case CandidateType.Department:
                        if (!string.IsNullOrWhiteSpace(candidate.CandidateId))
                        {
                            foreach (var departmentId in GetDepartmentScopeIds(candidate.CandidateId, candidate.CascadedDept))
                            {
                                deptIds.Add(departmentId);
                            }
                        }
                        break;
                    case CandidateType.EmployeeGroup:
                        if (!string.IsNullOrWhiteSpace(candidate.CandidateId))
                        {
                            employeeGroupIds.Add(candidate.CandidateId);
                        }
                        break;
                    case CandidateType.Employee:
                        if (!string.IsNullOrWhiteSpace(candidate.CandidateId))
                        {
                            empIds.Add(candidate.CandidateId);
                        }
                        break;
                    case CandidateType.Dynamic:
                        ExpandDynamicCandidate(dataContext, candidate, empIds, managerRequests);
                        break;
                    case CandidateType.FormField:
                        formData ??= GetFormData(dataContext.DataId);
                        formDef ??= GetFormDef(dataContext.FormId);
                        ExpandFormFieldCandidate(formData, formDef, candidate, empIds, deptIds, managerRequests);
                        break;
                }
            }

            if (deptIds.Count > 0)
            {
                var employeeIds = _employeeDepartmentRepository.Queryable
                    .Where(x => deptIds.Contains(x.DepartmentId))
                    .Select(x => x.EmployeeId)
                    .Distinct()
                    .ToList();
                var matched = await _employeeRepository
                    .Find(BuildActiveEmployeeFilter(x => employeeIds.Contains(x.Id)))
                    .ToListAsync();
                foreach (var employee in matched)
                {
                    empIds.Add(employee.Id);
                }
            }

            if (employeeGroupIds.Count > 0)
            {
                // 员工组归属由关系表 EmployeeGroupMember 承载，可用服务端查询直接求出组内员工
                // （原 jsonb 数组无法在服务端做元素级 EXISTS，只能拉回内存判断）。
                var groupEmployeeIds = (await _employeeRepository.Queryable
                    .SelectMany(x => x.Groups)
                    .Where(x => employeeGroupIds.Contains(x.EmployeeGroupId))
                    .Select(x => x.EmployeeId)
                    .Distinct()
                    .ToListAsync())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var scopedEmployeeIds = _employeeDepartmentRepository.Queryable
                    .Select(x => x.EmployeeId)
                    .Distinct()
                    .ToList();
                var scope = scopedEmployeeIds.Count == 0 || deptIds.Count > 0
                    ? null
                    : scopedEmployeeIds;

                var groupScopedEmployees = await _employeeRepository
                    .Find(BuildActiveEmployeeFilter(_ => true))
                    .ToListAsync();

                foreach (var employee in groupScopedEmployees)
                {
                    if (scope is not null && !scope.Contains(employee.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (groupEmployeeIds.Contains(employee.Id))
                    {
                        empIds.Add(employee.Id);
                    }
                }
            }

            if (managerRequests.Count > 0)
            {
                foreach (var pair in managerRequests)
                {
                    foreach (var managerId in await ResolveManagersByDepartmentAsync(pair.Key, pair.Value))
                    {
                        empIds.Add(managerId);
                    }
                }
            }

            return empIds.Take(100).ToList();
        }

        private void ExpandDynamicCandidate(
            WfDataContext dataContext,
            ApprovalCandidate candidate,
            ISet<string> empIds,
            IDictionary<string, HashSet<int>> managerRequests)
        {
            if (candidate.CandidateId == "starter" && dataContext.WfStarter != null)
            {
                empIds.Add(dataContext.WfStarter.Id);
                return;
            }

            if (!candidate.CandidateId.StartsWith("manager:", StringComparison.OrdinalIgnoreCase) || dataContext.WfStarter == null)
            {
                return;
            }

            var levels = NormalizeManagerLevels(candidate.ManagerLevels);
            if (levels.Count == 0)
            {
                return;
            }

            var starter = _employeeRepository.Get(dataContext.WfStarter.Id);
            if (starter == null)
            {
                return;
            }

            foreach (var departmentId in GetEmployeeDepartmentIds(starter.Id))
            {
                MergeManagerLevels(managerRequests, departmentId, levels);
            }
        }

        private void ExpandFormFieldCandidate(
            FormData data,
            FormDef formDef,
            ApprovalCandidate candidate,
            ISet<string> empIds,
            ISet<string> deptIds,
            IDictionary<string, HashSet<int>> managerRequests)
        {
            if (string.IsNullOrWhiteSpace(candidate.CandidateId))
            {
                return;
            }

            var fieldDef = formDef.Content.Items?.FirstOrDefault(x => x.Field.Equals(candidate.CandidateId, StringComparison.OrdinalIgnoreCase));
            if (fieldDef == null)
            {
                return;
            }

            var dataDict = (IDictionary<string, object?>)data.Data;
            if (!dataDict.TryGetValue(candidate.CandidateId, out var rawValue) || rawValue == null)
            {
                return;
            }

            var values = ExtractCandidateValues(rawValue);
            var managerLevels = NormalizeManagerLevels(candidate.ManagerLevels);
            switch (fieldDef.Type)
            {
                case FieldType.Department1:
                case FieldType.Department2:
                    foreach (var value in values)
                    {
                        if (managerLevels.Count > 0)
                        {
                            MergeManagerLevels(managerRequests, value, managerLevels);
                        }
                        else
                        {
                            foreach (var departmentId in GetDepartmentScopeIds(value, candidate.CascadedDept))
                            {
                                deptIds.Add(departmentId);
                            }
                        }
                    }
                    break;
                case FieldType.Employee1:
                case FieldType.Employee2:
                    if (managerLevels.Count > 0)
                    {
                        var employees = _employeeRepository
                            .Find(BuildActiveEmployeeFilter(x => values.Contains(x.Id)))
                            .ToList();
                        var employeeIds = employees.Select(x => x.Id).ToList();
                        var employeeDepartments = _employeeDepartmentRepository.Queryable
                            .Where(x => employeeIds.Contains(x.EmployeeId))
                            .Select(x => x.DepartmentId)
                            .Distinct()
                            .ToList();
                        foreach (var departmentId in employeeDepartments)
                        {
                            MergeManagerLevels(managerRequests, departmentId, managerLevels);
                        }
                    }
                    else
                    {
                        foreach (var value in values)
                        {
                            empIds.Add(value);
                        }
                    }
                    break;
            }
        }

        private async Task<List<string>> ResolveManagersByDepartmentAsync(string departmentId, IEnumerable<int> managerLevels)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(departmentId))
            {
                return [];
            }

            var targetLevels = new HashSet<int>(NormalizeManagerLevels(managerLevels));
            if (targetLevels.Count == 0)
            {
                return [];
            }

            var currentDepartmentId = departmentId;
            var currentLevel = 1;
            while (!string.IsNullOrWhiteSpace(currentDepartmentId) && targetLevels.Count > 0)
            {
                if (targetLevels.Contains(currentLevel))
                {
                    var managerEmployeeIds = _employeeDepartmentRepository.Queryable
                        .Where(x => x.DepartmentId == currentDepartmentId && x.IsManager)
                        .Select(x => x.EmployeeId)
                        .Distinct()
                        .ToList();
                    var managers = await _employeeRepository
                        .Find(BuildActiveEmployeeFilter(x => managerEmployeeIds.Contains(x.Id)))
                        .ToListAsync();
                    foreach (var manager in managers)
                    {
                        result.Add(manager.Id);
                    }

                    targetLevels.Remove(currentLevel);
                }

                var dept = _departmentRepository.Get(currentDepartmentId);
                currentDepartmentId = dept?.ParentId ?? string.Empty;
                currentLevel++;
            }

            return result.ToList();
        }

        private List<string> GetEmployeeDepartmentIds(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return [];
            }

            return _employeeDepartmentRepository.Queryable
                .Where(x => x.EmployeeId == employeeId)
                .Select(x => x.DepartmentId)
                .Distinct()
                .ToList();
        }

        private List<string> GetDepartmentScopeIds(string departmentId, bool cascaded)
        {
            if (string.IsNullOrWhiteSpace(departmentId))
            {
                return [];
            }

            // 直接用关系表上的层级路径快照匹配，省去先查 Department 表展开子部门、
            // 再按 DepartmentIds 查关系表的两次往返（快照由 DepartmentService 在层级变动时同步）。
            return _employeeDepartmentRepository.Queryable
                .Where(x => x.DepartmentId == departmentId
                    || (cascaded && x.HeriarchyId.Contains($"|{departmentId}|")))
                .Select(x => x.DepartmentId)
                .Distinct()
                .ToList();
        }

        private FormData GetFormData(string dataId)
        {
            return _formDataRepository.Get(dataId) ?? throw new InvalidOperationException("表单数据不存在");
        }

        private FormDef GetFormDef(string formId)
        {
            return _formDefRepository.Get(formId)
                ?? throw new InvalidOperationException("表单定义不存在");
        }

        /// <summary>
        /// 在给定条件上叠加「非虚拟 + 在职」的通用员工过滤。
        /// </summary>
        /// <param name="predicate">业务过滤谓词。</param>
        /// <returns>叠加后的过滤谓词。</returns>
        /// <remarks>
        /// 原实现是 <c>Builders&lt;Employee&gt;.Filter.And(IsDummy=false, Status=Active, filter)</c>；
        /// EF Core 下用表达式组合表达，语义一致。
        /// </remarks>
        private static Expression<Func<Employee, bool>> BuildActiveEmployeeFilter(Expression<Func<Employee, bool>> predicate)
        {
            return predicate.AndAlso(x => !x.IsDummy && x.Status == EmployeeStatus.Active);
        }

        private static List<int> NormalizeManagerLevels(IEnumerable<int>? levels)
        {
            return levels?
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList() ?? [];
        }

        private static void MergeManagerLevels(IDictionary<string, HashSet<int>> managerRequests, string departmentId, IEnumerable<int> levels)
        {
            if (string.IsNullOrWhiteSpace(departmentId))
            {
                return;
            }

            if (!managerRequests.TryGetValue(departmentId, out var set))
            {
                set = [];
                managerRequests[departmentId] = set;
            }

            foreach (var level in NormalizeManagerLevels(levels))
            {
                set.Add(level);
            }
        }

        private static List<string> ExtractCandidateValues(object rawValue)
        {
            var result = new List<string>();
            foreach (var item in EnumerateItemsOrSingle(rawValue))
            {
                var dict = item.AsDictionary();
                if (dict != null && dict.TryGetValue(Fields.Id, out var valueObj))
                {
                    var value = valueObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(value);
                    }
                }
                else
                {
                    var value = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(value);
                    }
                }
            }

            return result;
        }

        private static IEnumerable<object?> EnumerateItemsOrSingle(object? value)
        {
            if (value == null)
            {
                yield break;
            }

            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonElement.EnumerateArray())
                {
                    yield return item;
                }

                yield break;
            }

            if (value is IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    yield return item;
                }

                yield break;
            }

            yield return value;
        }
    }
}
