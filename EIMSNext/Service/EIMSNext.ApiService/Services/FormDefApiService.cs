using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Component;
using EIMSNext.Entities;

using EIMSNext.Service.Contracts;
using EIMSNext.Common;
using EIMSNext.Core.Services.Extensions;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 表单定义的 API 服务。
    /// </summary>
    /// <param name="resolver">服务解析器。</param>
    public class FormDefApiService(IResolver resolver) : ApiServiceBase<FormDef, IFormDefService>(resolver)
	{
        /// <summary>
        /// 获取FormsIncludeCross。
        /// </summary>
        public List<FormDef> GetFormsIncludeCross(string appId)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(appId);

            var ownForms = CoreService.All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.AppId == appId)
                .OrderBy(x => x.Name)
                .ToList()
                .Select(x => BuildView(x, external: false))
                .ToList();

            var bindings = Resolver.Resolve<ICrossBindingService>()
                .All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.TargetAppId == appId &&
                    x.SourceAppId != appId)
                .ToList();

            if (bindings.Count == 0)
            {
                return ownForms;
            }

            var sourceFormIds = bindings.Select(x => x.SourceFormId).Distinct().ToList();
            var sourceAppIds = bindings.Select(x => x.SourceAppId).Distinct().ToList();

            var accessibleSourceFormIds = sourceAppIds
                .SelectMany(sourceAppId => WorkbenchTargetResolver.GetAccessibleFormIds(Resolver, IdentityContext, sourceAppId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var apps = Resolver.Resolve<IAppDefService>().All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && sourceAppIds.Contains(x.Id))
                .ToDictionary(x => x.Id);
            var activeSourceAppIds = apps.Keys.ToList();

            var externalForms = CoreService.All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    sourceFormIds.Contains(x.Id) &&
                    activeSourceAppIds.Contains(x.AppId) &&
                    accessibleSourceFormIds.Contains(x.Id))
                .ToList()
                .Select(x => BuildView(x, external: true))
                .OrderBy(x => x.AppId)
                .ThenBy(x => x.Name)
                .ToList();

            ownForms.AddRange(externalForms);
            return ownForms;
        }

        /// <summary>
        /// 新增实体。
        /// </summary>
        public override Task AddAsync(FormDef entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);
            ValidateName(entity.Name);
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            ValidateFieldIds(entity.Content.Items);
            ValidateMemberSources(entity.Content.Items);
            PopulatePublicRelatedForms(entity);
            return base.AddAsync(entity);
        }

        /// <summary>
        /// 更新实体。
        /// </summary>
        public override Task<int> ReplaceAsync(FormDef entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);
            var existing = CoreService.Get(entity.Id);
            PublicFormSystemFieldHelper.EnsureExistingPublicFields(entity, existing?.Content);
            ValidateName(entity.Name);
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            ValidateFieldIds(entity.Content.Items);
            ValidateMemberSources(entity.Content.Items);
            PopulatePublicRelatedForms(entity);
            ServiceContext.ScopeCache.Set(entity.Id, entity, Cache.DataVersion.New);

            return base.ReplaceAsync(entity);
        }

        /// <summary>
        /// 执行 PurgeFieldChangeLogsAsync 操作。
        /// </summary>
        public async Task PurgeFieldChangeLogsAsync(string formId, IEnumerable<string>? fieldIds, bool clearAll)
        {
            var ids = fieldIds?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            if (clearAll == (ids.Count > 0))
            {
                throw new BadRequestException("彻底删除字段参数无效");
            }

            var form = CoreService.Get(formId);
            if (form == null || form.DeleteFlag || form.CorpId != IdentityContext.CurrentCorpId)
            {
                throw new BadRequestException("表单不存在");
            }

            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(form.AppId);
            await CoreService.PurgeFieldChangeLogsAsync(formId, ids, clearAll);
        }

        /// <summary>
        /// 删除实体核心逻辑。
        /// </summary>
        protected override async Task<int> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var forms = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (forms.Count != idList.Count)
            {
                throw new BadRequestException("表单不存在");
            }

            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            foreach (var form in forms)
            {
                evaluator.EnsureCanManageApp(form.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }

        private const int MaxNameLength = 100;

        private static void ValidateName(string name)
        {
            if (!string.IsNullOrEmpty(name) && name.Length > MaxNameLength)
            {
                throw new BadRequestException($"表单名称长度不能超过 {MaxNameLength} 个字符");
            }
        }

        private static void ValidateFieldIds(IEnumerable<FieldDef>? fields)
        {
            if (fields == null)
            {
                return;
            }

            foreach (var field in fields)
            {
                if (string.IsNullOrWhiteSpace(field.Field))
                {
                    throw new BadRequestException("字段 ID 不能为空");
                }

                ValidateFieldIds(field.Columns);
            }
        }

        private void ValidateMemberSources(IEnumerable<FieldDef>? fields)
        {
            foreach (var field in fields ?? [])
            {
                var source = field.Props.MemberSource;
                var items = source?.Items ?? [];
                ValidateMemberSourceItemTypes(field.Type, items);
                ValidateMemberSourceIds(field.Type, "department", items, ids =>
                    Resolver.GetRepository<Department>().Queryable
                        .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && ids.Contains(x.Id))
                        .Select(x => x.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase));
                ValidateMemberSourceIds(field.Type, "employeeGroup", items, ids =>
                    Resolver.GetRepository<EmployeeGroup>().Queryable
                        .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && ids.Contains(x.Id))
                        .Select(x => x.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase));
                ValidateMemberSourceIds(field.Type, "employee", items, ids =>
                    Resolver.GetRepository<Employee>().Queryable
                        .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && ids.Contains(x.Id))
                        .Select(x => x.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase));

                ValidateMemberSources(field.Columns);
            }
        }

        private static void ValidateMemberSourceItemTypes(string fieldType, IEnumerable<MemberSourceItem> items)
        {
            var isDepartmentField = fieldType is FieldType.Department1 or FieldType.Department2;
            foreach (var item in items)
            {
                var type = item.Type?.Trim().ToLowerInvariant();
                var id = item.Id?.Trim();
                var valid = type switch
                {
                    "department" => true,
                    "employee" or "employeegroup" => !isDepartmentField,
                    "dynamic" => string.Equals(id, "curdept", StringComparison.OrdinalIgnoreCase) ||
                                  (!isDepartmentField && string.Equals(id, "curuser", StringComparison.OrdinalIgnoreCase)),
                    _ => false,
                };

                if (!valid)
                {
                    throw new BadRequestException($"字段 {fieldType} 的成员数据源类型或动态参数无效");
                }
            }
        }

        private static void ValidateMemberSourceIds(
            string fieldType,
            string sourceType,
            IEnumerable<MemberSourceItem> items,
            Func<IReadOnlyCollection<string>, HashSet<string>> findExisting)
        {
            var ids = items
                .Where(x => x.Type.Equals(sourceType, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            HashSet<string> existing = ids.Length == 0 ? [] : findExisting(ids);
            foreach (var id in ids)
            {
                if (!existing.Contains(id))
                {
                    throw new BadRequestException($"字段 {fieldType} 的成员数据源 ID 不属于当前租户: {id}");
                }
            }
        }

        private void PopulatePublicRelatedForms(FormDef entity)
        {
            var relatedFormIds = FormRelatedSourceResolver.ResolveFormIds(entity.Content.Layout).ToList();
            if (relatedFormIds.Count == 0)
            {
                entity.PublicRelatedFormIds = [];
                return;
            }

            var accessibleFormIds = GetFormsIncludeCross(entity.AppId)
                .Select(x => x.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var inaccessible = relatedFormIds.Where(x => !accessibleFormIds.Contains(x)).ToList();
            if (inaccessible.Count > 0)
            {
                throw new BadRequestException($"关联数据源表单不可访问: {string.Join(',', inaccessible)}");
            }

            entity.PublicRelatedFormIds = relatedFormIds;
        }

        private static FormDef BuildView(FormDef form, bool external)
        {
            form.External = external;
            return form;
        }
    }
}
