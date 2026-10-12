using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using System.Collections;
using System.Text.Json;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// Resolves the data and field scope granted by a user's form read permissions.
    /// </summary>
    public sealed class FormDataReadScopeResolver(IResolver resolver) : ApiServiceBase(resolver)
    {
        /// <summary>
        /// 解析用户的表单读取权限所授予的数据与字段范围。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        /// <param name="permissionGroupId">权限组 ID。</param>
        /// <returns>表单数据读取范围。</returns>
        public FormDataReadScope Resolve(string formId, string? permissionGroupId = null)
        {
            if (Resolver.Resolve<TenantAccessEvaluator>().HasUnrestrictedManagementIdentity &&
                string.IsNullOrWhiteSpace(permissionGroupId))
            {
                return new FormDataReadScope(true, null, null);
            }

            var subjects = Resolver.Resolve<IEmployeeAccessSubjectResolver>().ResolveCurrent();
            if (string.IsNullOrWhiteSpace(subjects.EmployeeId))
            {
                return new FormDataReadScope(false, CreateNoMatchFilter(), []);
            }

            // Members 是 jsonb 列，EF Core 无法把它上面的 Any(...) + 客户端集合 Contains(...) 翻译成 SQL，
            // 因此这里先按可翻译的条件（租户/表单/删除/停用/权限组ID）在库内缩小范围，
            // 再把成员匹配放到内存执行。权限组数量很小，不会造成全表加载。
            var groups = Resolver.GetService<FormDataPermissionGroup>()
                .Query(group =>
                    group.CorpId == IdentityContext.CurrentCorpId &&
                    !group.DeleteFlag &&
                    !group.Disabled &&
                    (string.IsNullOrEmpty(formId) || group.FormId == formId))
                .Where(group => string.IsNullOrWhiteSpace(permissionGroupId) ||
                    group.Id == permissionGroupId)
                .ToList()
                .Where(group => group.Members.Any(member =>
                    (member.Type == MemberType.Employee && member.Id == subjects.EmployeeId) ||
                    (member.Type == MemberType.EmployeeGroup && subjects.EmployeeGroupIds.Contains(member.Id)) ||
                    (member.Type == MemberType.Department &&
                        (subjects.DepartmentIds.Contains(member.Id) ||
                         (member.CascadedDept && subjects.AncestorDepartmentIds.Contains(member.Id))))))
                .Where(group => GetEffectiveFormDataPermissions(group).HasFlag(FormDataPermissions.View))
                .ToList();
            if (groups.Count == 0)
            {
                return new FormDataReadScope(false, CreateNoMatchFilter(), []);
            }

            return new FormDataReadScope(true, BuildDataScopeFilter(groups, subjects), MergeFormFieldPermissions(groups));
        }

        private DynamicFilter? BuildDataScopeFilter(
            IEnumerable<FormDataPermissionGroup> permissionGroups,
            EmployeeAccessSubjects subjects)
        {
            var rangeFilters = new List<DynamicFilter>();
            DynamicMemberResolutionContext? dynamicContext = null;
            foreach (var permissionGroup in permissionGroups)
            {
                var groupFilter = BuildFormDataPermissionGroupDataFilter(permissionGroup, subjects, ref dynamicContext);
                if (groupFilter == null || groupFilter.IsEmpty)
                {
                    return null;
                }

                rangeFilters.Add(groupFilter);
            }

            return OrFilters(rangeFilters) ?? CreateNoMatchFilter();
        }

        private DynamicFilter? BuildFormDataPermissionGroupDataFilter(
            FormDataPermissionGroup permissionGroup,
            EmployeeAccessSubjects subjects,
            ref DynamicMemberResolutionContext? dynamicContext)
        {
            var dynamicResolutionFailed = false;
            var filter = permissionGroup.Type switch
            {
                FormDataPermissionMode.ManageSelfData => string.IsNullOrWhiteSpace(IdentityContext.CurrentEmployee?.Id)
                    ? CreateNoMatchFilter()
                    : new DynamicFilter
                    {
                        Field = Fields.CreateById,
                        Op = FilterOp.Eq,
                        Value = IdentityContext.CurrentEmployee.Id,
                    },
                FormDataPermissionMode.ViewAllData or FormDataPermissionMode.ManageAllData => null,
                FormDataPermissionMode.Custom when string.IsNullOrWhiteSpace(permissionGroup.DataFilter) => null,
                FormDataPermissionMode.Custom => permissionGroup.DataFilter!.DeserializeFromJson<ConditionList>()?.ToDynamicFilter(),
                _ => null,
            };

            filter = ResolveDynamicMemberValues(filter, subjects, ref dynamicContext, ref dynamicResolutionFailed);
            return dynamicResolutionFailed ? CreateNoMatchFilter() : filter;
        }

        /// <summary>
        /// 将单个权限组的数据过滤条件转换为当前用户可执行的过滤条件。
        /// 调用方负责先校验权限组归属和具体操作权限；此方法只负责条件解析。
        /// </summary>
        public DynamicFilter? ResolvePermissionGroupDataFilter(FormDataPermissionGroup permissionGroup)
        {
            var subjects = Resolver.Resolve<IEmployeeAccessSubjectResolver>().ResolveCurrent();
            DynamicMemberResolutionContext? dynamicContext = null;
            return BuildFormDataPermissionGroupDataFilter(permissionGroup, subjects, ref dynamicContext);
        }

        private DynamicFilter? ResolveDynamicMemberValues(
            DynamicFilter? filter,
            EmployeeAccessSubjects subjects,
            ref DynamicMemberResolutionContext? dynamicContext,
            ref bool dynamicResolutionFailed)
        {
            if (filter == null)
            {
                return null;
            }

            if (filter.IsGroup)
            {
                var items = filter.Items ?? [];
                for (var index = 0; index < items.Count; index++)
                {
                    items[index] = ResolveDynamicMemberValues(items[index], subjects, ref dynamicContext, ref dynamicResolutionFailed)
                        ?? CreateNoMatchFilter();
                }

                return filter;
            }

            if (!IsMemberField(filter.Type))
            {
                return filter;
            }

            var memberIds = ExtractMemberIds(filter.Value).ToList();
            if (memberIds.Count == 0)
            {
                return filter;
            }

            var resolved = new List<string>();
            var hadDynamicValue = false;
            var resolvedDynamicValue = false;
            foreach (var memberId in memberIds)
            {
                switch (memberId.ToLowerInvariant())
                {
                    case "curuser":
                        hadDynamicValue = true;
                        if (!string.IsNullOrWhiteSpace(subjects.EmployeeId) && IsEmployeeField(filter.Type))
                        {
                            resolved.Add(subjects.EmployeeId);
                            resolvedDynamicValue = true;
                        }
                        break;
                    case "curdept":
                        hadDynamicValue = true;
                        dynamicContext ??= LoadDynamicMemberResolutionContext(subjects);
                        if (IsDepartmentField(filter.Type) && dynamicContext.MainDepartmentId != null)
                        {
                            resolved.Add(dynamicContext.MainDepartmentId);
                            resolvedDynamicValue = true;
                        }
                        break;
                    default:
                        resolved.Add(memberId);
                        break;
                }
            }

            if (hadDynamicValue && !resolvedDynamicValue)
            {
                // Do not represent a failed dynamic value as a normal false leaf.
                // A false leaf inside a `not` group would become true and could
                // accidentally turn a permission filter into an unrestricted one.
                dynamicResolutionFailed = true;
                return CreateNoMatchFilter();
            }

            filter.Value = IsMultiValue(filter.Value)
                ? resolved.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : resolved.FirstOrDefault();
            return filter;
        }

        private DynamicMemberResolutionContext LoadDynamicMemberResolutionContext(EmployeeAccessSubjects subjects)
        {
            var mainDepartmentId = Resolver.GetRepository<EmployeeDepartment>().Queryable
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId &&
                            !x.DeleteFlag &&
                            x.EmployeeId == subjects.EmployeeId)
                .OrderBy(x => x.SortValue)
                .ThenBy(x => x.Id)
                .Select(x => x.DepartmentId)
                .FirstOrDefault();

            return new DynamicMemberResolutionContext(mainDepartmentId);
        }

        private static bool IsMemberField(string? type) =>
            IsEmployeeField(type) || IsDepartmentField(type);

        private static bool IsEmployeeField(string? type) =>
            string.Equals(type, FieldType.Employee1, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, FieldType.Employee2, StringComparison.OrdinalIgnoreCase);

        private static bool IsDepartmentField(string? type) =>
            string.Equals(type, FieldType.Department1, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, FieldType.Department2, StringComparison.OrdinalIgnoreCase);

        private static bool IsMultiValue(object? value) =>
            value is IEnumerable && value is not string && value is not IDictionary;

        private static IEnumerable<string> ExtractMemberIds(object? value)
        {
            if (value is null)
            {
                yield break;
            }

            if (value is string text)
            {
                if (!string.IsNullOrWhiteSpace(text)) yield return text;
                yield break;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (string.Equals(entry.Key?.ToString(), "id", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(entry.Value?.ToString()))
                    {
                        yield return entry.Value!.ToString()!;
                        yield break;
                    }
                }

                yield break;
            }

            if (value is IEnumerable sequence)
            {
                foreach (var item in sequence)
                {
                    foreach (var id in ExtractMemberIds(item)) yield return id;
                }
            }
        }

        private static DynamicFilter CreateNoMatchFilter()
        {
            return new DynamicFilter
            {
                Field = Fields.Id,
                Op = FilterOp.Eq,
                Value = "__no_permission__",
            };
        }

        private static DynamicFilter? OrFilters(IEnumerable<DynamicFilter?> filters)
        {
            var list = filters
                .Where(x => x != null && !x.IsEmpty)
                .Cast<DynamicFilter>()
                .ToList();
            return list.Count switch
            {
                0 => null,
                1 => list[0],
                _ => new DynamicFilter { Rel = FilterRel.Or, Items = list },
            };
        }

        private static List<FormFieldPermission>? MergeFormFieldPermissions(IEnumerable<FormDataPermissionGroup> permissionGroups)
        {
            var groups = permissionGroups.ToList();
            if (groups.Any(x => x.FormFieldPermissions == null || x.FormFieldPermissions.Count == 0))
            {
                return null;
            }

            var merged = new Dictionary<string, FormFieldPermission>(StringComparer.OrdinalIgnoreCase);
            foreach (var fieldPerm in groups.SelectMany(x => x.FormFieldPermissions))
            {
                if (!merged.TryGetValue(fieldPerm.Id, out var current))
                {
                    merged[fieldPerm.Id] = new FormFieldPermission
                    {
                        Id = fieldPerm.Id,
                        Visible = fieldPerm.Visible,
                        Editable = fieldPerm.Editable,
                        TableInsert = fieldPerm.TableInsert,
                        TableEdit = fieldPerm.TableEdit,
                        TableDelete = fieldPerm.TableDelete,
                    };
                    continue;
                }

                current.Visible |= fieldPerm.Visible;
                current.Editable |= fieldPerm.Editable;
                current.TableInsert = MergeNullablePermission(current.TableInsert, fieldPerm.TableInsert);
                current.TableEdit = MergeNullablePermission(current.TableEdit, fieldPerm.TableEdit);
                current.TableDelete = MergeNullablePermission(current.TableDelete, fieldPerm.TableDelete);
            }

            return merged.Values.ToList();
        }

        private static bool? MergeNullablePermission(bool? current, bool? next)
        {
            if (current == true || next == true)
            {
                return true;
            }

            return current.HasValue || next.HasValue ? false : null;
        }

        private static FormDataPermissions GetEffectiveFormDataPermissions(FormDataPermissionGroup permissionGroup)
        {
            return permissionGroup.Type switch
            {
                FormDataPermissionMode.ManageSelfData or FormDataPermissionMode.ManageAllData => FormDataPermissions.All,
                FormDataPermissionMode.ViewAllData => FormDataPermissions.View,
                _ => (FormDataPermissions)permissionGroup.FormDataPermissions,
            };
        }

        private sealed record DynamicMemberResolutionContext(string? MainDepartmentId);
    }

    /// <summary>
    /// 表单数据读取范围。
    /// </summary>
    /// <param name="CanRead">是否可读。</param>
    /// <param name="DataFilter">数据过滤条件。</param>
    /// <param name="FormFieldPermissions">表单字段权限列表。</param>
    public sealed record FormDataReadScope(bool CanRead, DynamicFilter? DataFilter, List<FormFieldPermission>? FormFieldPermissions);
}
