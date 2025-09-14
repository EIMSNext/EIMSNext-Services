
using EIMSNext.ApiClient.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using RestSharp;

namespace EIMSNext.ApiClient.Flow
{
    public class FlowApiClient : RestApiClientBase<FlowApiClient, FlowApiClientSetting>
    {
        public FlowApiClient(IConfiguration config, ILogger<FlowApiClient> logger) : base(config, logger)
        {
        }

        public async Task<WfResponse?> Approve(ApproveRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Approve", req, accessToken));
        }

        public  async Task<WfResponse?> Status(StatusRequest req, string accessToken)
        {
            var query = string.IsNullOrEmpty(req.WfInstanceId) ? $"dataId={req.DataId}" : $"wfInstanceId={req.WfInstanceId}";
            return HandleResponse(await GetAsync<WfResponse>($"Workflow/Status/?{query}", accessToken));
        }

        public async Task<WfResponse?> Load(LoadDefRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Load", req, accessToken));
        }

        public async Task<WfResponse?> Start(StartRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Start", req, accessToken));
        }

        public async Task<WfResponse?> RunDataflow(DfRunRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Dataflow/Run", req, accessToken));
        }

        private WfResponse HandleResponse(RestResponse<WfResponse> response)
        {
            if (response.IsSuccessful)
            {
                return response.Data!;
            }
            else
            {
                return new WfResponse() { Id = response.StatusCode.ToString(), Error = response.ErrorMessage };
            }
        }
    }
}
