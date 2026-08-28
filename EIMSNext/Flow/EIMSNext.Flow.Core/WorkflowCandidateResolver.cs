using System.Collections;
using System.Dynamic;
using System.Text.Json;

using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;

using MongoDB.Driver;

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
                await _employeeRepository.Find(new MongoFindOptions<Employee>
                {
                    Filter = BuildActiveEmployeeFilter(Builders<Employee>.Filter.In(x => x.Id, employeeIds))
                })
                    .ForEachAsync(x => empIds.Add(x.Id));
            }

            if (employeeGroupIds.Count > 0)
            {
                await _employeeRepository.Find(new MongoFindOptions<Employee>
                {
                    Filter = BuildActiveEmployeeFilter(Builders<Employee>.Filter.ElemMatch(x => x.EmployeeGroups, r => employeeGroupIds.Contains(r.EmployeeGroupId)))
                }).ForEachAsync(x => empIds.Add(x.Id));
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
                        var employees = _employeeRepository.Find(new MongoFindOptions<Employee>
                        {
                            Filter = BuildActiveEmployeeFilter(Builders<Employee>.Filter.In(x => x.Id, values))
                        }).ToList();
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
                    await _employeeRepository.Find(new MongoFindOptions<Employee>
                    {
                        Filter = BuildActiveEmployeeFilter(Builders<Employee>.Filter.In(x => x.Id, managerEmployeeIds))
                    })
                        .ForEachAsync(x => result.Add(x.Id));
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

            var query = _departmentRepository.Queryable
                .Where(x => !x.DeleteFlag && x.Id == departmentId);
            if (cascaded)
            {
                query = _departmentRepository.Queryable
                    .Where(x => !x.DeleteFlag && (x.Id == departmentId || x.HeriarchyId.Contains($"|{departmentId}|")));
            }

            return query.Select(x => x.Id).Distinct().ToList();
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

        private static FilterDefinition<Employee> BuildActiveEmployeeFilter(FilterDefinition<Employee> filter)
        {
            return Builders<Employee>.Filter.And(
                Builders<Employee>.Filter.Eq(x => x.IsDummy, false),
                Builders<Employee>.Filter.Eq(x => x.Status, EmployeeStatus.Active),
                filter);
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
                var dict = AsDictionary(item);
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

        private static IDictionary<string, object?>? AsDictionary(object? value)
        {
            if (value is ExpandoObject expandoObject)
            {
                return (IDictionary<string, object?>)expandoObject;
            }

            if (value is IDictionary<string, object?> dict)
            {
                return dict;
            }

            if (value is IDictionary<string, object> objectDict)
            {
                return objectDict.ToDictionary(x => x.Key, x => (object?)x.Value);
            }

            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                var expando = jsonElement.ToString().DeserializeFromJson<ExpandoObject>();
                return expando == null ? null : (IDictionary<string, object?>)expando;
            }

            return null;
        }
    }
}
