using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using System.Text.Json;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// Resolves the data and field scope granted by a user's form read permissions.
    /// </summary>
    public sealed class FormDataReadScopeResolver(IResolver resolver) : ApiServiceBase(resolver)
    {
        public FormDataReadScope Resolve(string formId, string? authGroupId = null)
        {
            if (Resolver.Resolve<AdminPermissionEvaluator>().HasUnrestrictedManagementIdentity &&
                string.IsNullOrWhiteSpace(authGroupId))
            {
                return new FormDataReadScope(true, null, null);
            }

            var groups = Resolver.Resolve<AdminPermissionEvaluator>()
                .GetUsageAuthGroupsForCurrentEmployee(formId)
                .Where(group => string.IsNullOrWhiteSpace(authGroupId) ||
                    string.Equals(group.Id, authGroupId, StringComparison.OrdinalIgnoreCase))
                .Where(group => GetEffectiveDataPerms(group).HasFlag(DataPerms.View))
                .ToList();
            if (groups.Count == 0)
            {
                return new FormDataReadScope(false, CreateNoMatchFilter(), []);
            }

            return new FormDataReadScope(true, BuildDataScopeFilter(groups), MergeFieldPerms(groups));
        }

        private DynamicFilter? BuildDataScopeFilter(IEnumerable<AuthGroup> authGroups)
        {
            var rangeFilters = new List<DynamicFilter>();
            foreach (var authGroup in authGroups)
            {
                var groupFilter = BuildAuthGroupDataFilter(authGroup);
                if (groupFilter == null || groupFilter.IsEmpty)
                {
                    return null;
                }

                rangeFilters.Add(groupFilter);
            }

            return OrFilters(rangeFilters) ?? CreateNoMatchFilter();
        }

        private DynamicFilter? BuildAuthGroupDataFilter(AuthGroup authGroup)
        {
            return authGroup.Type switch
            {
                AuthGroupType.ManageSelfData => string.IsNullOrWhiteSpace(IdentityContext.CurrentEmployee?.Id)
                    ? CreateNoMatchFilter()
                    : new DynamicFilter
                    {
                        Field = Fields.CreateById,
                        Op = FilterOp.Eq,
                        Value = IdentityContext.CurrentEmployee.Id,
                    },
                AuthGroupType.ViewAllData or AuthGroupType.ManageAllData => null,
                AuthGroupType.Custom when string.IsNullOrWhiteSpace(authGroup.DataFilter) => null,
                AuthGroupType.Custom => authGroup.DataFilter!.DeserializeFromJson<ConditionList>()?.ToDynamicFilter(),
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

        private static List<FieldPerm>? MergeFieldPerms(IEnumerable<AuthGroup> authGroups)
        {
            var groups = authGroups.ToList();
            if (groups.Any(x => x.FieldPerms == null || x.FieldPerms.Count == 0))
            {
                return null;
            }

            var merged = new Dictionary<string, FieldPerm>(StringComparer.OrdinalIgnoreCase);
            foreach (var fieldPerm in groups.SelectMany(x => x.FieldPerms))
            {
                if (!merged.TryGetValue(fieldPerm.Id, out var current))
                {
                    merged[fieldPerm.Id] = new FieldPerm
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

        private static DataPerms GetEffectiveDataPerms(AuthGroup authGroup)
        {
            return authGroup.Type switch
            {
                AuthGroupType.ManageSelfData or AuthGroupType.ManageAllData => DataPerms.All,
                AuthGroupType.ViewAllData => DataPerms.View,
                _ => (DataPerms)authGroup.DataPerms,
            };
        }
    }

    public sealed record FormDataReadScope(bool CanRead, DynamicFilter? DataFilter, List<FieldPerm>? FieldPerms);
}
