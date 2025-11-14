using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using EIMSNext.ApiClient.Flow;

using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class WfDefinitionApiService : ApiServiceBase<Wf_Definition, WfDefinitionViewModel>
    {
        private FlowApiClient _flowClient;
        public WfDefinitionApiService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
        }

        public override async Task AddAsync(Wf_Definition entity)
        {
            await base.AddAsync(entity);
            await _flowClient.Load(new LoadDefRequest { WfDefinitionId = entity.ExternalId, Version = entity.Version }, IdentityContext.AccessToken);
        }
        public override async Task<ReplaceOneResult> ReplaceAsync(Wf_Definition entity)
        {
            var result = await base.ReplaceAsync(entity);
            await _flowClient.Load(new LoadDefRequest { WfDefinitionId = entity.ExternalId, Version = entity.Version }, IdentityContext.AccessToken);
            return result;
        }
    }
}
