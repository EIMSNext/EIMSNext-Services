
using EIMSNext.ApiClient.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using RestSharp;

namespace EIMSNext.ApiClient.Flow
{
    public class FlowApiClient : RestApiClientBase<FlowApiClient, FlowApiClientSetting>, IFlowClient
    {
        public FlowApiClient(IConfiguration config, ILogger<FlowApiClient> logger) : base(config, logger)
        {
        }

        public async Task<WfResponse?> Terminate(TerminateRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Terminate", req, accessToken));
        }

        public async Task<WfResponse?> ChangeApprover(ChangeApproverRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/ChangeApprover", req, accessToken));
        }

        public async Task<WfResponse?> DeleteDef(DeleteRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Definition/Delete", req, accessToken));
        }
        public async Task<WfResponse?> Approve(ApproveRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Approve", req, accessToken));
        }

        public async Task<WfResponse?> Submit(ApproveRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Submit", req, accessToken));
        }

        public async Task<WfResponse?> Reject(ApproveRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Reject", req, accessToken));
        }

        public async Task<WfResponse?> Return(ReturnRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Return", req, accessToken));
        }

        public async Task<WfResponse?> AddSign(AddSignRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/AddSign", req, accessToken));
        }

        public async Task<WfResponse?> Transfer(TransferRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Transfer", req, accessToken));
        }

        public async Task<WfResponse?> Withdraw(WithdrawRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Withdraw", req, accessToken));
        }

        public async Task<WfResponse?> Urge(UrgeRequest req, string accessToken)
        {
            return HandleResponse(await PostAsync<WfResponse>("Workflow/Urge", req, accessToken));
        }

        public async Task<WfActionStatusResponse?> ActionStatus(ActionStatusRequest req, string accessToken)
        {
            var query = string.IsNullOrEmpty(req.WfInstanceId)
                ? $"dataId={req.DataId}"
                : $"dataId={req.DataId}&wfInstanceId={req.WfInstanceId}";
            var response = await GetAsync<WfActionStatusResponse>($"Workflow/ActionStatus/?{query}", accessToken);
            if (response.IsSuccessful)
            {
                return response.Data!;
            }

            return new WfActionStatusResponse { Error = response.ErrorMessage };
        }

        public async Task<List<ReturnTargetNode>?> ReturnNodes(ActionStatusRequest req, string accessToken)
        {
            var query = string.IsNullOrEmpty(req.WfInstanceId)
                ? $"dataId={req.DataId}"
                : $"dataId={req.DataId}&wfInstanceId={req.WfInstanceId}";
            var response = await GetAsync<List<ReturnTargetNode>>("Workflow/ReturnNodes/?" + query, accessToken);
            return response.IsSuccessful ? response.Data! : [];
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
