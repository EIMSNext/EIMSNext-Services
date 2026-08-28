using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using System.Text.Json;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// Resolves the data and field scope granted by a user's form read permissions.
    /// </summary>
    public sealed class FormDataReadScopeResolver(IResolver resolver) : ApiServiceBase(resolver)
    {
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

            var groups = Resolver.GetService<FormDataPermissionGroup>()
                .Query(group =>
                    group.CorpId == IdentityContext.CurrentCorpId &&
                    !group.DeleteFlag &&
                    !group.Disabled &&
                    (string.IsNullOrEmpty(formId) || group.FormId == formId) &&
                    group.Members.Any(member =>
                        (member.Type == MemberType.Employee && member.Id == subjects.EmployeeId) ||
                        (member.Type == MemberType.EmployeeGroup && subjects.EmployeeGroupIds.Contains(member.Id)) ||
                        (member.Type == MemberType.Department &&
                            (subjects.DepartmentIds.Contains(member.Id) ||
                             (member.CascadedDept && subjects.AncestorDepartmentIds.Contains(member.Id))))))
                .Where(group => string.IsNullOrWhiteSpace(permissionGroupId) ||
                    string.Equals(group.Id, permissionGroupId, StringComparison.OrdinalIgnoreCase))
                .Where(group => GetEffectiveFormDataPermissions(group).HasFlag(FormDataPermissions.View))
                .ToList();
            if (groups.Count == 0)
            {
                return new FormDataReadScope(false, CreateNoMatchFilter(), []);
            }

            return new FormDataReadScope(true, BuildDataScopeFilter(groups), MergeFormFieldPermissions(groups));
        }

        private DynamicFilter? BuildDataScopeFilter(IEnumerable<FormDataPermissionGroup> permissionGroups)
        {
            var rangeFilters = new List<DynamicFilter>();
            foreach (var permissionGroup in permissionGroups)
            {
                var groupFilter = BuildFormDataPermissionGroupDataFilter(permissionGroup);
                if (groupFilter == null || groupFilter.IsEmpty)
                {
                    return null;
                }

                rangeFilters.Add(groupFilter);
            }

            return OrFilters(rangeFilters) ?? CreateNoMatchFilter();
        }

        private DynamicFilter? BuildFormDataPermissionGroupDataFilter(FormDataPermissionGroup permissionGroup)
        {
            return permissionGroup.Type switch
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
        }

        private static DynamicFilter CreateNoMatchFilter()
        {
            return new DynamicFilter
            {
                Field = Fields.BsonId,
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
    }

    public sealed record FormDataReadScope(bool CanRead, DynamicFilter? DataFilter, List<FormFieldPermission>? FormFieldPermissions);
}
