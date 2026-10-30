using EIMSNext.ApiClient.Flow;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Abstractions.System;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using System.Net;

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
            if (string.IsNullOrWhiteSpace(args.CorpId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(args.EventFlowId) && !args.MatchAllEventFlows)
            {
                Logger.LogError("EventFlow 消息缺少 EventFlowId 且未声明广播匹配，已拒绝执行。ExecutionId={ExecutionId}", args.ExecutionId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(args.RunLogId) && string.IsNullOrWhiteSpace(args.RetryAttemptId))
            {
                Logger.LogError("EF 重试消息缺少 RetryAttemptId，已拒绝执行。RunLogId={RunLogId}", args.RunLogId);
                return;
            }

            if (string.IsNullOrWhiteSpace(args.ExecutionId))
            {
                Logger.LogError("EventFlow 消息缺少 ExecutionId，已拒绝执行。DataId={DataId}", args.DataId);
                return;
            }

            IMessageProcessingRepository processingRepository;
            string eventKey;
            try
            {
                processingRepository = resolver.Resolve<IMessageProcessingRepository>();
                eventKey = resolver.Resolve<IOutboxIdempotencyKeyFactory>().Create(args);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "EventFlow 消费基础设施不可用，将重新投递。ExecutionId={ExecutionId}", args.ExecutionId);
                throw new TaskRequeueException("EventFlow 消费基础设施不可用。", TimeSpan.FromSeconds(30));
            }
            const string target = "eventflow-run";
            string? leaseToken;
            try
            {
                leaseToken = await processingRepository.TryAcquireAsync(
                    eventKey,
                    target,
                    DateTime.UtcNow.AddMinutes(5),
                    ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "EventFlow 幂等记录不可用，将重新投递。key={Key}", eventKey);
                throw new TaskRequeueException("EventFlow 幂等记录不可用。", TimeSpan.FromSeconds(30));
            }
            if (leaseToken == null)
            {
                try
                {
                    if (await processingRepository.IsCompletedAsync(eventKey, target, ct))
                    {
                        Logger.LogInformation("EventFlow 消息已处理，跳过。key={Key}", eventKey);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "EventFlow 幂等状态不可用，将重新投递。key={Key}", eventKey);
                    throw new TaskRequeueException("EventFlow 幂等状态不可用。", TimeSpan.FromSeconds(30));
                }

                throw new TaskRequeueException(
                    "EventFlow 仍持有处理中租约，等待租约过期后重新接管。",
                    TimeSpan.FromMinutes(5.5));
            }

            try
            {
                var flowClient = resolver.Resolve<FlowApiClient>();
                var tokenProvider = resolver.Resolve<ISystemTokenProvider>();
                var tokenObjectId = string.IsNullOrWhiteSpace(args.EventFlowId)
                    ? args.ExecutionId
                    : args.EventFlowId;
                var accessToken = await tokenProvider.GetAccessTokenAsync(args.CorpId, "ef", tokenObjectId, ct);
                var response = await flowClient.RunEventFlow(
                    new EfRunRequest
                    {
                        EventFlowId = args.EventFlowId,
                        MatchAllEventFlows = args.MatchAllEventFlows,
                        ExecutionId = args.ExecutionId,
                        RunLogId = args.RunLogId,
                        RetryNodeId = args.RetryNodeId,
                        RetryAttemptId = args.RetryAttemptId,
                        DataId = args.DataId ?? string.Empty,
                        EventSource = MapEventSource(args.EventSource),
                        EventType = MapEventType(args.EventType),
                        WfNodeId = args.WfNodeId ?? string.Empty,
                        NodeAction = args.NodeAction,
                        StarterId = args.StarterId,
                        StarterCode = args.StarterCode,
                        StarterName = args.StarterName,
                        ChainJson = args.ChainJson,
                        ChangeFields = args.ChangeFields,
                    },
                    accessToken: accessToken);

                if (IsTransientFlowHostFailure(response))
                {
                    Logger.LogError(
                        "Flow.Host 调用失败，将重新投递。EventFlowId={EventFlowId}, DataId={DataId}, Status={Status}, Error={Error}",
                        args.EventFlowId,
                        args.DataId,
                        response?.Id ?? "no response",
                        response?.Error ?? "Flow.Host 未返回执行结果");
                    await ReleaseLeaseAsync(processingRepository, eventKey, target, leaseToken);
                    throw new TaskRequeueException("Flow.Host 暂时不可用。", TimeSpan.FromSeconds(30));
                }

                if (!string.IsNullOrEmpty(response?.Error))
                {
                    Logger.LogError(
                        "EventFlow 业务执行失败，记录失败结果并确认消息。EventFlowId={EventFlowId}, DataId={DataId}, Error={Error}",
                        args.EventFlowId,
                        args.DataId,
                        response.Error);
                    var completed = await processingRepository.MarkCompletedAsync(
                        eventKey,
                        target,
                        leaseToken,
                        DateTime.UtcNow.ToTimeStampMs(),
                        ct);
                    if (!completed)
                        Logger.LogError("EventFlow 业务失败已返回，但未能写入幂等终态。key={Key}", eventKey);
                    return;
                }

                var markedCompleted = await processingRepository.MarkCompletedAsync(
                    eventKey,
                    target,
                    leaseToken,
                    DateTime.UtcNow.ToTimeStampMs(),
                    ct);
                if (!markedCompleted)
                    Logger.LogWarning("EventFlow 运行成功，但幂等租约已被接管，未覆盖新租约。key={Key}", eventKey);
            }
            catch (Exception ex)
            {
                await ReleaseLeaseAsync(processingRepository, eventKey, target, leaseToken);
                Logger.LogError(ex,
                    "EventFlow 调用基础设施失败，将重新投递。EventFlowId={EventFlowId}, DataId={DataId}",
                    args.EventFlowId, args.DataId);
                throw new TaskRequeueException("EventFlow 执行失败。", TimeSpan.FromSeconds(30));
            }
        }

        private async Task ReleaseLeaseAsync(
            IMessageProcessingRepository processingRepository,
            string eventKey,
            string target,
            string leaseToken)
        {
            try
            {
                await processingRepository.ReleaseAsync(eventKey, target, leaseToken, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "释放 EventFlow 消费租约失败。key={Key}", eventKey);
            }
        }

        internal static bool IsTransientFlowHostFailure(WfResponse? response)
        {
            if (response == null)
            {
                return true;
            }

            if (string.Equals(response.Id, "0", StringComparison.Ordinal))
            {
                return true;
            }

            if (!int.TryParse(response.Id, out var statusCode))
            {
                if (!Enum.TryParse<HttpStatusCode>(response.Id, true, out var status) || !Enum.IsDefined(status))
                {
                    return false;
                }

                statusCode = (int)status;
            }

            return statusCode is 408 or 429 or >= 500 and <= 599;
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

    }
}
