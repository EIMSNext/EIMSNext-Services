using HKH.Mef2.Integration;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using MongoDB.Driver;
using EIMSNext.ApiClient.Flow;
using EIMSNext.Core;
using EIMSNext.Core.Query;

namespace EIMSNext.Service
{
    public class AppService : EntityServiceBase<App>, IAppService
    {
        private FlowApiClient _flowClient;
        public AppService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
        }

        protected override async Task AfterDelete(FilterDefinition<App> filter, IClientSessionHandle? session)
        {
            await base.AfterDelete(filter, session);

            var deletedApps = Repository.Find(new MongoFindOptions<App> { Filter = filter }, session).ToList();
            if (deletedApps.Count == 0)
                return;

            //基本上App每次只删除一个
            deletedApps.ForEach(async app =>
            {
                //更新所有表单定义为已删除
                var formDefRepo = Resolver.GetRepository<FormDef>();
                await formDefRepo.UpdateManyAsync(formDefRepo.FilterBuilder.And(formDefRepo.FilterBuilder.Eq(x => x.DeleteFlag, false), formDefRepo.FilterBuilder.Eq(x => x.AppId, app.Id)), formDefRepo.UpdateBuilder.Set(x => x.DeleteFlag, true), session: session);

                //更新所有相关数据为已删除
                var formDataRepo = Resolver.GetRepository<FormData>();
                await formDataRepo.UpdateManyAsync(formDataRepo.FilterBuilder.And(formDataRepo.FilterBuilder.Eq(x => x.DeleteFlag, false), formDataRepo.FilterBuilder.Eq(x => x.AppId, app.Id)), formDataRepo.UpdateBuilder.Set(x => x.DeleteFlag, true), session: session);

                //删除所有待办
                var todoRepo = Resolver.GetRepository<Wf_Todo>();
                await todoRepo.DeleteAsync(todoRepo.FilterBuilder.Eq(x => x.AppId, app.Id), session);

                //废弃所有流程实例
                await _flowClient.DeleteDef(new DeleteRequest { DeleteDef = true, AppId = app.Id }, Context.AccessToken);
            });
        }
    }
}
