using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
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

namespace EIMSNext.Service
{
    public class AppDefService : EntityServiceBase<AppDef>, IAppDefService
    {
        private readonly FlowApiClient _flowClient;
        public AppDefService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
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

                await _flowClient.DeleteDef(new DeleteRequest { DeleteDef = true, AppId = appId }, Context.AccessToken);
            }
        }
    }
}
