using EIMSNext.ApiClient.Flow;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Tasks.System;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Async.Tasks.Consumers
{
    /// <summary>
    /// EventFlow 调度执行消费者。订阅 eventflow-run 队列，调用 FlowApiClient.RunEventFlow 在 Flow.Host 触发一次执行。
    /// </summary>
    public class EventFlowRunConsumer : TaskConsumerBase<EventFlowRunTaskArgs, EventFlowRunConsumer>
    {
        public EventFlowRunConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }

        protected override async Task HandleAsync(EventFlowRunTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(args.CorpId) || string.IsNullOrWhiteSpace(args.EventFlowId))
            {
                return;
            }

            var flowClient = resolver.Resolve<FlowApiClient>();
            var tokenProvider = resolver.Resolve<ISystemTokenProvider>();
            try
            {
                var accessToken = await tokenProvider.GetAccessTokenAsync(args.CorpId, "ef", args.EventFlowId, ct);
                var response = await flowClient.RunEventFlow(
                    new EfRunRequest
                    {
                        EventFlowId = args.EventFlowId,
                        DataId = args.DataId ?? string.Empty,
                        EventSource = MapEventSource(args.EventSource),
                        EventType = MapEventType(args.EventType),
                        WfNodeId = args.WfNodeId ?? string.Empty,
                        EfCascade = MapCascade(args.Cascade),
                    },
                    accessToken: accessToken);

                if (response != null && !string.IsNullOrEmpty(response.Error))
                {
                    Logger.LogError(
                        "EventFlow run failed. EventFlowId={EventFlowId}, DataId={DataId}, Error={Error}",
                        args.EventFlowId, args.DataId, response.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "EventFlow run threw. EventFlowId={EventFlowId}, DataId={DataId}",
                    args.EventFlowId, args.DataId);
            }
        }

        private static ApiClient.Flow.EventSourceType MapEventSource(EIMSNext.Entities.EventSourceType source)
        {
            return source switch
            {
                EIMSNext.Entities.EventSourceType.Form => ApiClient.Flow.EventSourceType.Form,
                EIMSNext.Entities.EventSourceType.Http => ApiClient.Flow.EventSourceType.Http,
                EIMSNext.Entities.EventSourceType.Schedule => ApiClient.Flow.EventSourceType.Schedule,
                EIMSNext.Entities.EventSourceType.Button => ApiClient.Flow.EventSourceType.Button,
                _ => ApiClient.Flow.EventSourceType.None,
            };
        }

        private static ApiClient.Flow.EventType MapEventType(EIMSNext.Entities.EventType type)
        {
            return type switch
            {
                EIMSNext.Entities.EventType.Submitted => ApiClient.Flow.EventType.Submitted,
                EIMSNext.Entities.EventType.Modified => ApiClient.Flow.EventType.Modified,
                EIMSNext.Entities.EventType.Removed => ApiClient.Flow.EventType.Removed,
                EIMSNext.Entities.EventType.Approving => ApiClient.Flow.EventType.Approving,
                EIMSNext.Entities.EventType.Approved => ApiClient.Flow.EventType.Approved,
                EIMSNext.Entities.EventType.Rejected => ApiClient.Flow.EventType.Rejected,
                _ => ApiClient.Flow.EventType.None,
            };
        }

        private static ApiClient.Flow.CascadeMode MapCascade(EIMSNext.Entities.CascadeMode mode)
        {
            return mode switch
            {
                EIMSNext.Entities.CascadeMode.All => ApiClient.Flow.CascadeMode.All,
                EIMSNext.Entities.CascadeMode.Specified => ApiClient.Flow.CascadeMode.Specified,
                EIMSNext.Entities.CascadeMode.Never => ApiClient.Flow.CascadeMode.Never,
                _ => ApiClient.Flow.CascadeMode.NotSet,
            };
        }
    }
}
