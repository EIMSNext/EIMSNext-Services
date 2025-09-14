using EIMSNext.ApiClient.Flow;
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class FormDataApiService : ApiServiceBase<FormData, FormData>
    {
        private FlowApiClient _flowClient;
        private IFormDefService _formDefService;
        private IWfDefinitionService _wfDefinitionService;
        private IServiceContext _serviceContext;
        public FormDataApiService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
            _formDefService = resolver.Resolve<IFormDefService>();
            _wfDefinitionService = resolver.Resolve<IWfDefinitionService>();
            _serviceContext = resolver.GetServiceContext();
        }

        public override async Task AddAsync(FormData entity)
        {
            _serviceContext.Action = DataAction.Submit;
            await base.AddAsync(entity);
            
            return;
        }
    }
}
