using System.Collections;
using System.Dynamic;
using System.Text.Json;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using EIMSNext.Async.Abstractions.Messaging;

using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class FormNotifyRecipientResolver(IResolver resolver) : IFormNotifyRecipientResolver
    {
        private IRepository<Employee> EmployeeRepository => resolver.GetRepository<Employee>();

        public async Task<List<NotifyReceiver>> ResolveAsync(FormData data, FormDef formDef, string? notifiersJson, string? operatorEmpId)
        {
            var receivers = new Dictionary<string, NotifyReceiver>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(notifiersJson))
            {
                return receivers.Values.ToList();
            }

            var notifiers = notifiersJson.DeserializeFromJson<List<ApprovalCandidate>>() ?? [];
            var deptIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var empIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var notifier in notifiers)
            {
                switch (notifier.CandidateType)
                {
                    case CandidateType.Department:
                        if (!string.IsNullOrWhiteSpace(notifier.CandidateId))
                        {
                            deptIds.Add(notifier.CandidateId);
                        }
                        break;
                    case CandidateType.Role:
                        if (!string.IsNullOrWhiteSpace(notifier.CandidateId))
                        {
                            roleIds.Add(notifier.CandidateId);
                        }
                        break;
                    case CandidateType.Employee:
                        if (!string.IsNullOrWhiteSpace(notifier.CandidateId))
                        {
                            empIds.Add(notifier.CandidateId);
                        }
                        break;
                    case CandidateType.FormField:
                        ExpandFormFieldCandidate(data, formDef, notifier.CandidateId, deptIds, empIds);
                        break;
                }
            }

            if (deptIds.Count > 0)
            {
                await EmployeeRepository.Find(x => deptIds.Contains(x.DepartmentId)).ForEachAsync(x => empIds.Add(x.Id));
            }

            if (roleIds.Count > 0)
            {
                await EmployeeRepository.Find(new MongoFindOptions<Employee>
                {
                    Filter = Builders<Employee>.Filter.ElemMatch(x => x.Roles, r => roleIds.Contains(r.RoleId))
                }).ForEachAsync(x => empIds.Add(x.Id));
            }

            await EmployeeRepository.Find(x => empIds.Contains(x.Id) && !x.IsDummy && x.Status == 0).ForEachAsync(x =>
            {
                //TODO: 暂时不排除当前操作人，方便测试
                //if (x.Id.Equals(operatorEmpId, StringComparison.OrdinalIgnoreCase))
                //{
                //    return;
                //}

                if (!receivers.ContainsKey(x.Id))
                {
                    receivers[x.Id] = new NotifyReceiver
                    {
                        EmpId = x.Id,
                        EmpName = x.EmpName
                    };
                }
            });

            return receivers.Values.Take(200).ToList();
        }

        private static void ExpandFormFieldCandidate(FormData data, FormDef formDef, string fieldKey, ISet<string> deptIds, ISet<string> empIds)
        {
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
                    deptIds.Add(value);
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
