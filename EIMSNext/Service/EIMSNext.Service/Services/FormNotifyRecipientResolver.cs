using System.Collections;
using System.Dynamic;
using System.Text.Json;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Async.Abstractions.Messaging;

using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using EIMSNext.Core.Extensions;

namespace EIMSNext.Service
{
    public class FormNotifyRecipientResolver(IResolver resolver) : IFormNotifyRecipientResolver
    {
        private IRepository<Employee> EmployeeRepository => resolver.GetRepository<Employee>();
        private IRepository<EmployeeDepartment> EmployeeDepartmentRepository => resolver.GetRepository<EmployeeDepartment>();
        private IRepository<Department> DepartmentRepository => resolver.GetRepository<Department>();

        public async Task<List<NotifyReceiver>> ResolveAsync(FormData data, FormDef formDef, string? notifiersJson, string? operatorEmpId)
        {
            if (string.IsNullOrWhiteSpace(notifiersJson))
            {
                return [];
            }

            var notifiers = notifiersJson.DeserializeFromJson<List<ApprovalCandidate>>() ?? [];
            return await ResolveCandidatesAsync(data, formDef, notifiers, operatorEmpId);
        }

        public async Task<List<NotifyReceiver>> ResolveCandidatesAsync(FormData data, FormDef formDef, IEnumerable<ApprovalCandidate> candidates, string? operatorEmpId)
        {
            return await ResolveCandidatesInternalAsync(candidates, operatorEmpId, (notifier, deptIds, empIds) =>
            {
                if (notifier.CandidateType == CandidateType.FormField)
                {
                    ExpandFormFieldCandidate(data, formDef, notifier, deptIds, empIds);
                }
            });
        }

        public async Task<List<NotifyReceiver>> ResolveCandidatesAsync(IEnumerable<ApprovalCandidate> candidates, string? operatorEmpId)
        {
            return await ResolveCandidatesInternalAsync(
                candidates.Where(x => x.CandidateType != CandidateType.FormField),
                operatorEmpId,
                null);
        }

        private async Task<List<NotifyReceiver>> ResolveCandidatesInternalAsync(
            IEnumerable<ApprovalCandidate> candidates,
            string? operatorEmpId,
            Action<ApprovalCandidate, ISet<string>, ISet<string>>? expandDynamicCandidate)
        {
            var receivers = new Dictionary<string, NotifyReceiver>(StringComparer.OrdinalIgnoreCase);
            var deptIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var employeeGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var empIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var notifier in candidates)
            {
                switch (notifier.CandidateType)
                {
                    case CandidateType.Department:
                        if (!string.IsNullOrWhiteSpace(notifier.CandidateId))
                        {
                            foreach (var departmentId in GetDepartmentScopeIds(notifier.CandidateId, notifier.CascadedDept))
                            {
                                deptIds.Add(departmentId);
                            }
                        }
                        break;
                    case CandidateType.EmployeeGroup:
                        if (!string.IsNullOrWhiteSpace(notifier.CandidateId))
                        {
                            employeeGroupIds.Add(notifier.CandidateId);
                        }
                        break;
                    case CandidateType.Employee:
                        if (!string.IsNullOrWhiteSpace(notifier.CandidateId))
                        {
                            empIds.Add(notifier.CandidateId);
                        }
                        break;
                    case CandidateType.FormField:
                        expandDynamicCandidate?.Invoke(notifier, deptIds, empIds);
                        break;
                }
            }

            // 对应 Mongo 版的 filters.Count == 0 短路：三类候选都为空时不查库直接返回。
            if (empIds.Count == 0 && deptIds.Count == 0 && employeeGroupIds.Count == 0)
            {
                return [];
            }

            // 员工候选与部门候选最终都是「Id 命中」，可合并为同一个集合。
            // Mongo 版用 In(x => x.Id, empIds)，字符串精确（大小写敏感）匹配；这里保持一致。
            // 注意快路径走 SQL IN（数据库端大小写敏感），内存路径也必须大小写敏感，否则两条路径行为不一致。
            var empIdFilter = empIds.ToHashSet();
            if (deptIds.Count > 0)
            {
                var departmentEmployeeIds = EmployeeDepartmentRepository.Queryable
                    .Where(x => deptIds.Contains(x.DepartmentId))
                    .Select(x => x.EmployeeId)
                    .Distinct()
                    .ToList();
                foreach (var employeeId in departmentEmployeeIds)
                {
                    empIdFilter.Add(employeeId);
                }
            }

            if (employeeGroupIds.Count == 0)
            {
                // 只有员工/部门候选，全部可下推到数据库。
                var matched = EmployeeRepository.Queryable
                    .Where(x => !x.IsDummy && x.Status == 0 && empIdFilter.Contains(x.Id))
                    .ToList();
                foreach (var employee in matched)
                {
                    AddReceiver(receivers, employee);
                }

                return receivers.Values.Take(200).ToList();
            }

            // 员工组候选由关系表 EmployeeGroupMember 承载（jsonb 投影 Employee.EmployeeGroups
            // 已移除），先在服务端求出属于任一目标员工组的员工。注意三类候选之间是「或」关系：
            // 不能只对已按 empIdFilter 过滤过的结果再筛员工组。
            var groupIdList = employeeGroupIds.ToList();
            var groupEmployeeIds = EmployeeRepository.Queryable
                .SelectMany(x => x.Groups)
                .Where(x => groupIdList.Contains(x.EmployeeGroupId))
                .Select(x => x.EmployeeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var employee in EmployeeRepository.Queryable.Where(x => !x.IsDummy && x.Status == 0).ToList())
            {
                if (empIdFilter.Contains(employee.Id) || groupEmployeeIds.Contains(employee.Id))
                {
                    AddReceiver(receivers, employee);
                }
            }

            return receivers.Values.Take(200).ToList();
        }

        private static void AddReceiver(IDictionary<string, NotifyReceiver> receivers, Employee employee)
        {
            //TODO: 暂时不排除当前操作人，方便测试
            //if (employee.Id.Equals(operatorEmpId, StringComparison.OrdinalIgnoreCase))
            //{
            //    return;
            //}

            if (!receivers.ContainsKey(employee.Id))
            {
                receivers[employee.Id] = new NotifyReceiver
                {
                    EmpId = employee.Id,
                    EmpName = employee.EmpName,
                    Email = employee.WorkEmail
                };
            }
        }

        private void ExpandFormFieldCandidate(FormData data, FormDef formDef, ApprovalCandidate notifier, ISet<string> deptIds, ISet<string> empIds)
        {
            var fieldKey = notifier.CandidateId;
            var fieldDef = formDef.Content.Items?.FirstOrDefault(x => x.Field.Equals(fieldKey, StringComparison.OrdinalIgnoreCase));
            if (fieldDef == null)
            {
                return;
            }

            var dataDict = (IDictionary<string, object?>)data.Data;
            if (!dataDict.TryGetValue(fieldKey, out var rawValue) || rawValue == null)
            {
                return;
            }

            var values = ExtractCandidateValues(rawValue);
            if (fieldDef.Type == FieldType.Department1 || fieldDef.Type == FieldType.Department2)
            {
                foreach (var value in values)
                {
                    foreach (var departmentId in GetDepartmentScopeIds(value, notifier.CascadedDept))
                    {
                        deptIds.Add(departmentId);
                    }
                }
            }
            else if (fieldDef.Type == FieldType.Employee1 || fieldDef.Type == FieldType.Employee2)
            {
                foreach (var value in values)
                {
                    empIds.Add(value);
                }
            }
        }

        private List<string> GetDepartmentScopeIds(string departmentId, bool cascaded)
        {
            if (string.IsNullOrWhiteSpace(departmentId))
            {
                return [];
            }

            // 直接用关系表上的层级路径快照匹配，省去先查 Department 表展开子部门、
            // 再按 DepartmentIds 查关系表的两次往返（快照由 DepartmentService 在层级变动时同步）。
            return EmployeeDepartmentRepository.Queryable
                .Where(x => x.DepartmentId == departmentId
                    || (cascaded && x.HeriarchyId.Contains($"|{departmentId}|")))
                .Select(x => x.DepartmentId)
                .Distinct()
                .ToList();
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
