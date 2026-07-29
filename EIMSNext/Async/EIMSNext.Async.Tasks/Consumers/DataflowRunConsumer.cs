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
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Async.Tasks.Consumers
{
    /// <summary>
    /// 数据流调度执行消费者。订阅 dataflow-run 队列，调用 FlowApiClient.RunDataflow 在 Flow.Host 触发一次数据流执行。
    /// </summary>
    public class DataflowRunConsumer : TaskConsumerBase<DataflowRunTaskArgs, DataflowRunConsumer>
    {
        public DataflowRunConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }

        protected override async Task HandleAsync(DataflowRunTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(args.CorpId) || string.IsNullOrWhiteSpace(args.DataflowId))
            {
                return;
            }

            var flowClient = resolver.Resolve<FlowApiClient>();
            var tokenProvider = resolver.Resolve<ISystemTokenProvider>();
            try
            {
                var accessToken = await tokenProvider.GetAccessTokenAsync(args.CorpId, "df", args.DataflowId, ct);
                var response = await flowClient.RunDataflow(
                    new DfRunRequest
                    {
                        DataflowId = args.DataflowId,
                        DataId = args.DataId ?? string.Empty,
                        EventSource = MapEventSource(args.EventSource),
                        EventType = MapEventType(args.EventType),
                        WfNodeId = args.WfNodeId ?? string.Empty,
                        DfCascade = MapCascade(args.Cascade),
                    },
                    accessToken: accessToken);

                if (response != null && !string.IsNullOrEmpty(response.Error))
                {
                    Logger.LogError(
                        "Dataflow run failed. DataflowId={DataflowId}, DataId={DataId}, Error={Error}",
                        args.DataflowId, args.DataId, response.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Dataflow run threw. DataflowId={DataflowId}, DataId={DataId}",
                    args.DataflowId, args.DataId);
            }
        }

        private static ApiClient.Flow.EventSourceType MapEventSource(Service.Entities.EventSourceType source)
        {
            return source switch
            {
                Service.Entities.EventSourceType.Form => ApiClient.Flow.EventSourceType.Form,
                Service.Entities.EventSourceType.Http => ApiClient.Flow.EventSourceType.Http,
                Service.Entities.EventSourceType.Schedule => ApiClient.Flow.EventSourceType.Schedule,
                Service.Entities.EventSourceType.Button => ApiClient.Flow.EventSourceType.Button,
                _ => ApiClient.Flow.EventSourceType.None,
            };
        }

        private static ApiClient.Flow.EventType MapEventType(Service.Entities.EventType type)
        {
            return type switch
            {
                Service.Entities.EventType.Submitted => ApiClient.Flow.EventType.Submitted,
                Service.Entities.EventType.Modified => ApiClient.Flow.EventType.Modified,
                Service.Entities.EventType.Removed => ApiClient.Flow.EventType.Removed,
                Service.Entities.EventType.Approving => ApiClient.Flow.EventType.Approving,
                Service.Entities.EventType.Approved => ApiClient.Flow.EventType.Approved,
                Service.Entities.EventType.Rejected => ApiClient.Flow.EventType.Rejected,
                _ => ApiClient.Flow.EventType.None,
            };
        }

        private static ApiClient.Flow.CascadeMode MapCascade(Service.Entities.CascadeMode mode)
        {
            return mode switch
            {
                Service.Entities.CascadeMode.All => ApiClient.Flow.CascadeMode.All,
                Service.Entities.CascadeMode.Specified => ApiClient.Flow.CascadeMode.Specified,
                Service.Entities.CascadeMode.Never => ApiClient.Flow.CascadeMode.Never,
                _ => ApiClient.Flow.CascadeMode.NotSet,
            };
        }
    }
}
