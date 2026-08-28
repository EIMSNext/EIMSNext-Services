using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;
using EIMSNext.ApiClient.Flow;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Service
{
    public class AppDefService : EntityServiceBase<AppDef>, IAppDefService
    {
        private readonly FlowApiClient _flowClient;
        public AppDefService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
        }

        public override async Task<object> DeleteAsync(string id)
        {
            var appIds = GetAppIds(FilterBuilder.Eq(x => x.Id, id));
            var result = await base.DeleteAsync(id);
            await DeleteFlowDefinitionsAfterCommitAsync(appIds);
            return result;
        }

        public override async Task<object> DeleteAsync(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var appIds = GetAppIds(FilterBuilder.In(x => x.Id, idList));
            var result = await base.DeleteAsync(idList);
            await DeleteFlowDefinitionsAfterCommitAsync(appIds);
            return result;
        }

        public override async Task<object> DeleteAsync(DynamicFilter filter)
        {
            var mongoFilter = filter.ToFilterDefinition<AppDef>();
            var appIds = GetAppIds(mongoFilter);
            var result = await base.DeleteAsync(filter);
            await DeleteFlowDefinitionsAfterCommitAsync(appIds);
            return result;
        }

        protected override async Task AfterDelete(FilterDefinition<AppDef> filter, IClientSessionHandle? session)
        {
            await base.AfterDelete(filter, session);

            var deletedAppIds = Repository.Find(new MongoFindOptions<AppDef> { Filter = filter }, session)
                .Project(x => x.Id)
                .ToList();
            if (deletedAppIds.Count == 0)
                return;

            foreach (var appId in deletedAppIds)
            {
                var formDefRepo = Resolver.GetRepository<FormDef>();
                var formIds = formDefRepo.Find(x => x.AppId == appId && !x.DeleteFlag)
                    .Project(x => x.Id)
                    .ToList();
                if (formIds.Count > 0)
                {
                    await Resolver.Resolve<IFormDefService>().DeleteAsync(formIds);
                }

                var dashboardRepo = Resolver.GetRepository<DashboardDef>();
                var dashboardIds = dashboardRepo.Find(x => x.AppId == appId && !x.DeleteFlag)
                    .Project(x => x.Id)
                    .ToList();
                if (dashboardIds.Count > 0)
                {
                    await Resolver.Resolve<IDashboardDefService>().DeleteAsync(dashboardIds);
                }

            }
        }

        private List<string> GetAppIds(FilterDefinition<AppDef> filter)
        {
            return Repository.Collection.Find(filter)
                .Project(x => x.Id)
                .ToList()
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task DeleteFlowDefinitionsAfterCommitAsync(IReadOnlyCollection<string> appIds)
        {
            foreach (var appId in appIds)
            {
                try
                {
                    var response = await _flowClient.DeleteDef(
                        new DeleteRequest { DeleteDef = true, AppId = appId },
                        Context.AccessToken);

                    if (!string.IsNullOrWhiteSpace(response?.Error))
                    {
                        Logger.LogError(
                            "Flow definition cleanup returned an error after app deletion. CorpId={CorpId}, AppId={AppId}, Error={Error}",
                            Context.CorpId,
                            appId,
                            response.Error);

                        // TODO: 将来通过系统消息通知系统维保人员处理流程定义清理失败。
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(
                        ex,
                        "Failed to delete Flow definitions after app deletion. CorpId={CorpId}, AppId={AppId}",
                        Context.CorpId,
                        appId);

                    // TODO: 将来通过系统消息通知系统维保人员处理流程定义清理失败。
                }
            }
        }
    }
}
