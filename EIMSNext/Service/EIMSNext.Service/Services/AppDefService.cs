using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;
using EIMSNext.ApiClient.Flow;
using EIMSNext.Core;
using EIMSNext.Core.Query;

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

            var deletedApps = Repository.Find(new MongoFindOptions<AppDef> { Filter = filter }, session).ToList();
            if (deletedApps.Count == 0)
                return;

            deletedApps.ForEach(async app =>
            {
                var formDefRepo = Resolver.GetRepository<FormDef>();
                await formDefRepo.UpdateManyAsync(formDefRepo.FilterBuilder.And(formDefRepo.FilterBuilder.Eq(x => x.DeleteFlag, false), formDefRepo.FilterBuilder.Eq(x => x.AppId, app.Id)), formDefRepo.UpdateBuilder.Set(x => x.DeleteFlag, true), session: session);

                var formDataRepo = Resolver.GetRepository<FormData>();
                await formDataRepo.UpdateManyAsync(formDataRepo.FilterBuilder.And(formDataRepo.FilterBuilder.Eq(x => x.DeleteFlag, false), formDataRepo.FilterBuilder.Eq(x => x.AppId, app.Id)), formDataRepo.UpdateBuilder.Set(x => x.DeleteFlag, true), session: session);

                var todoRepo = Resolver.GetRepository<Wf_Todo>();
                await todoRepo.DeleteAsync(todoRepo.FilterBuilder.Eq(x => x.AppId, app.Id), session);

                await _flowClient.DeleteDef(new DeleteRequest { DeleteDef = true, AppId = app.Id }, Context.AccessToken);
            });
        }
    }
}
