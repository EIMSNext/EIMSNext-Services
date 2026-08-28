using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Component;
using EIMSNext.Entities;

using MongoDB.Driver;
using EIMSNext.Service.Contracts;
using EIMSNext.Common;

namespace EIMSNext.ApiService
{
    public class FormDefApiService(IResolver resolver) : ApiServiceBase<FormDef, FormDefViewModel, IFormDefService>(resolver)
	{
        public List<FormDefViewModel> GetFormsIncludeCross(string appId)
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

        public override Task AddAsync(FormDef entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            ValidateFieldIds(entity.Content.Items);
            PopulatePublicRelatedForms(entity);
            return base.AddAsync(entity);
        }

        public override Task<ReplaceOneResult> ReplaceAsync(FormDef entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);
            var existing = CoreService.Get(entity.Id);
            PublicFormSystemFieldHelper.EnsureExistingPublicFields(entity, existing?.Content);
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            ValidateFieldIds(entity.Content.Items);
            PopulatePublicRelatedForms(entity);
            ServiceContext.ScopeCache.Set(entity.Id, entity, Cache.DataVersion.New);

            return base.ReplaceAsync(entity);
        }

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

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
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

        private static FormDefViewModel BuildView(FormDef form, bool external)
        {
            return new FormDefViewModel
            {
                Id = form.Id,
                CorpId = form.CorpId,
                CreateBy = form.CreateBy,
                CreateTime = form.CreateTime,
                UpdateBy = form.UpdateBy,
                UpdateTime = form.UpdateTime,
                DeleteFlag = form.DeleteFlag,
                AppId = form.AppId,
                TemplateId = form.TemplateId,
                Name = form.Name,
                Content = form.Content,
                UsingWorkflow = form.UsingWorkflow,
                FormSettings = form.FormSettings,
                External = external,
            };
        }
    }
}
