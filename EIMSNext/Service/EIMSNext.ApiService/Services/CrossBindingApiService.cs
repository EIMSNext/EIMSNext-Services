using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class CrossBindingApiService(IResolver resolver) : ApiServiceBase<CrossBinding, CrossBindingViewModel, ICrossBindingService>(resolver)
    {
        protected override IQueryable<CrossBindingViewModel> FilterByPermission()
        {
            var query = base.FilterByPermission();
            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return query;
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                var appIds = evaluator.GetSnapshot().ManageableAppIds;
                return query.Where(x => appIds.Contains(x.TargetAppId));
            }

            return query.Where(x => false);
        }

        protected override Task AddAsyncCore(CrossBinding entity)
        {
            ValidateBindingTarget(entity.TargetAppId, entity.SourceAppId, entity.SourceFormId);
            EnsureNotDuplicated(entity);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(CrossBinding entity)
        {
            ValidateBindingTarget(entity.TargetAppId, entity.SourceAppId, entity.SourceFormId);
            EnsureNotDuplicated(entity);
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var bindings = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (bindings.Count != idList.Count)
            {
                throw new BadRequestException("跨应用绑定不存在");
            }

            foreach (var binding in bindings)
            {
                Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(binding.TargetAppId);
            }

            return await base.DeleteAsyncCore(idList);
        }

        private void ValidateBindingTarget(string targetAppId, string sourceAppId, string sourceFormId)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(targetAppId);

            if (string.IsNullOrWhiteSpace(sourceAppId))
            {
                throw new BadRequestException("来源应用ID不能为空");
            }

            if (targetAppId == sourceAppId)
            {
                throw new BadRequestException("跨应用绑定不能指向当前应用");
            }

            if (string.IsNullOrWhiteSpace(sourceFormId))
            {
                throw new BadRequestException("来源表单ID不能为空");
            }

            var visibleAppIds = WorkbenchTargetResolver.GetAccessibleAppIds(Resolver, IdentityContext);
            if (!visibleAppIds.Contains(sourceAppId))
            {
                throw new ForbiddenException("没有访问来源应用的权限");
            }

            var form = Resolver.GetService<IFormDefService, FormDef>().Get(sourceFormId);
            if (form == null || form.CorpId != IdentityContext.CurrentCorpId || form.DeleteFlag || form.AppId != sourceAppId)
            {
                throw new BadRequestException("来源表单不存在");
            }

            var visibleFormIds = WorkbenchTargetResolver.GetAccessibleFormIds(Resolver, IdentityContext, sourceAppId);
            if (!visibleFormIds.Contains(sourceFormId))
            {
                throw new ForbiddenException("没有访问来源表单的权限");
            }
        }

        private void EnsureNotDuplicated(CrossBinding entity)
        {
            var duplicated = CoreService.All().Any(x =>
                x.CorpId == IdentityContext.CurrentCorpId &&
                !x.DeleteFlag &&
                x.Id != entity.Id &&
                x.TargetAppId == entity.TargetAppId &&
                x.SourceFormId == entity.SourceFormId);
            if (duplicated)
            {
                throw new BadRequestException("跨应用表单已绑定");
            }
        }
    }
}
