using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Entities;
using EIMSNext.ApiClient.Flow;

using MongoDB.Driver;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
    public class WfDefinitionApiService : ApiServiceBase<Wf_Definition, WfDefinitionViewModel, IWfDefinitionService>
    {
        private FlowApiClient _flowClient;
        public WfDefinitionApiService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
        }

        public override async Task AddAsync(Wf_Definition entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            await base.AddAsync(entity);
            await _flowClient.Load(new LoadDefRequest { WfDefinitionId = entity.ExternalId, Version = entity.Version }, IdentityContext.AccessToken);
        }
        public override async Task<ReplaceOneResult> ReplaceAsync(Wf_Definition entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            var result = await base.ReplaceAsync(entity);
            await _flowClient.Load(new LoadDefRequest { WfDefinitionId = entity.ExternalId, Version = entity.Version }, IdentityContext.AccessToken);
            return result;
        }

        public async Task<Wf_Definition> CreateVersionAsync(string id)
        {
            var source = GetManageableDefinition(id);
            var result = await Resolver.Resolve<IWfDefinitionService>().CreateVersionAsync(source.Id);
            await _flowClient.Load(new LoadDefRequest { WfDefinitionId = result.ExternalId, Version = result.Version }, IdentityContext.AccessToken);
            return result;
        }

        public async Task<Wf_Definition> ActivateAsync(string id)
        {
            var source = GetManageableDefinition(id);
            var result = await Resolver.Resolve<IWfDefinitionService>().ActivateAsync(source.Id);
            await _flowClient.Load(new LoadDefRequest { WfDefinitionId = result.ExternalId, Version = result.Version }, IdentityContext.AccessToken);
            return result;
        }

        protected override IQueryable<WfDefinitionViewModel> FilterByPermission()
        {
            var query = base.FilterByPermission();
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return query;
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                var appIds = evaluator.GetSnapshot().ManageableAppIds;
                return query.Where(x => appIds.Contains(x.AppId));
            }

            return query.Where(x => false);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var definitions = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (definitions.Count != idList.Count)
            {
                throw new BadRequestException("流程定义不存在");
            }

            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            foreach (var definition in definitions)
            {
                evaluator.EnsureCanManageApp(definition.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }

        private Wf_Definition GetManageableDefinition(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new BadRequestException("流程定义ID不能为空");
            }

            var definition = CoreService.Get(id);
            if (definition == null || definition.CorpId != IdentityContext.CurrentCorpId || definition.DeleteFlag)
            {
                throw new BadRequestException("流程定义不存在");
            }

            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(definition.AppId);
            return definition;
        }
    }
}
