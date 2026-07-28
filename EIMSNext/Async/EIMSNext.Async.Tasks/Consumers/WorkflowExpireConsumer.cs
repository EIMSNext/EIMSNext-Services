using EIMSNext.ApiClient.Flow;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Tasks.System;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace EIMSNext.Async.Tasks.Consumers
{
    public class WorkflowExpireConsumer : TaskConsumerBase<WorkflowExpireTaskArgs, WorkflowExpireConsumer>
    {
        public WorkflowExpireConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }

        protected override async Task HandleAsync(WorkflowExpireTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(args.WfInstanceId) || string.IsNullOrWhiteSpace(args.DataId) || string.IsNullOrWhiteSpace(args.WfNodeId))
            {
                return;
            }

            var flowClient = resolver.Resolve<FlowApiClient>();
            var tokenProvider = resolver.Resolve<ISystemTokenProvider>();
            var accessToken = await tokenProvider.GetAccessTokenAsync(args.CorpId, "wf", args.WfInstanceId, ct);
            var response = await flowClient.ExpireAction(new ExpireActionRequest
            {
                WfInstanceId = args.WfInstanceId,
                DataId = args.DataId,
                WfNodeId = args.WfNodeId,
                ActionType = MapActionType(args.ActionType),
            }, accessToken);

            if (response != null && !string.IsNullOrWhiteSpace(response.Error))
            {
                Logger.LogError("Workflow expire action failed. WfInstanceId={WfInstanceId}, DataId={DataId}, NodeId={NodeId}, Error={Error}",
                    args.WfInstanceId, args.DataId, args.WfNodeId, response.Error);
                return;
            }

            if (args.TodoIds.Count > 0)
            {
                var todoRepo = resolver.GetRepository<Wf_Todo>();
                todoRepo.UpdateMany(
                    Builders<Wf_Todo>.Filter.In(x => x.Id, args.TodoIds),
                    Builders<Wf_Todo>.Update
                        .Set(x => x.ExpireHandled, true)
                        .Set(x => x.UpdateTime, DateTime.UtcNow.ToTimeStampMs()),
                    upsert: false);
            }
        }

        private static ApiClient.Flow.WfExpireActionType MapActionType(Service.Entities.WfExpireActionType actionType)
        {
            return actionType switch
            {
                Service.Entities.WfExpireActionType.AutoApprove => ApiClient.Flow.WfExpireActionType.AutoApprove,
                Service.Entities.WfExpireActionType.AutoTransfer => ApiClient.Flow.WfExpireActionType.AutoTransfer,
                Service.Entities.WfExpireActionType.AutoReject => ApiClient.Flow.WfExpireActionType.AutoReject,
                Service.Entities.WfExpireActionType.AutoReturn => ApiClient.Flow.WfExpireActionType.AutoReturn,
                _ => ApiClient.Flow.WfExpireActionType.AutoNotify,
            };
        }
    }
}
