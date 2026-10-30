
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Entities;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Scripting;
using System.Text.Json;
using HKH.Mef2.Integration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using WorkflowCore.Interface;
using EIMSNext.Core.Extensions;
using EIMSNext.Component;

namespace EIMSNext.Flow.Core.Nodes
{
    public class EventFlowRunner : IEventFlowRunner
    {
        private readonly IResolver _resolver;

        public EventFlowRunner(IResolver resolver)
        {
            _resolver = resolver;
            ScriptEngine = resolver.Resolve<IScriptEngine>();
            Logger = resolver.GetLogger<EventFlowRunner>();
            var maxDepthRaw = resolver.Resolve<IConfiguration>()[EfChainConsts.MaxChainDepthConfigKey];
            MaxChainDepth = EfChainConsts.ResolveMaxChainDepth(maxDepthRaw);
        }

        protected ISyncWorkflowRunner SyncWfRunner => _resolver.Resolve<ISyncWorkflowRunner>();
        protected IScriptEngine ScriptEngine { get; private set; }
        protected ILogger<EventFlowRunner> Logger { get; private set; }
        protected int MaxChainDepth { get; private set; }


        public bool IsMeet(Wf_Definition eventFlow, FormData data)
        {
            if (eventFlow.EventSource == EventSourceType.Form)
            {
                var triggerSetting = eventFlow.Metadata.Steps.First().EfNodeSetting?.TriggerSetting;

                if (!string.IsNullOrEmpty(triggerSetting?.Condition))
                {
                    return ScriptEngine.Evaluate<bool>(triggerSetting.Condition, data.ToScriptData()).Value;
                }
            }

            return true;
        }

        public async Task<EfExecResult> RunAsync(EfRunParameter paramter)
        {
            var execResult = new EfExecResult();
            if (string.IsNullOrWhiteSpace(paramter.ExecutionId))
            {
                paramter.WithExecutionId(TsidIdGenerator.NewId());
            }

            if (paramter.Chain is { } incomingChain && !incomingChain.IsValid(MaxChainDepth))
            {
                Logger.LogWarning(
                    "EventFlow 链上下文无效，按新链处理。ExecutionId={ExecutionId}, ChainId={ChainId}, Depth={Depth}",
                    paramter.ExecutionId,
                    incomingChain.ChainId,
                    incomingChain.Depth);
                paramter.WithChain(null);
            }

            // 出站级联门禁（链尾解析）：以「当前执行的 EF」——链尾定义——上的配置为准，各是各的，与链起始点无关。
            // 链为空 = 入口触发（表单/定时/HTTP/手动 API），不设门禁；入站只看 trigger 配置。
            var cascadeGate = await ResolveChainCascadeAsync(paramter);
            if (cascadeGate.BlockReason != null)
            {
                // 级联开关切断：逐候选写运行日志留痕（治「为啥没跑」），不静默返回
                await WriteCascadeBlockedLogsAsync(paramter, cascadeGate.BlockReason);
                return execResult;
            }
            var cascadeAllowedIds = cascadeGate.AllowedIds;

            var repository = _resolver.Resolve<IRepository<Wf_Definition>>();
            var candidates = LoadCandidates(repository, paramter);

            // 父链 = 本次触发携带的链（null = 新链）。每个候选都从父链独立派生：
            // 同批候选是兄弟关系，不得互相把对方并入 Path/抬升 Depth（否则深度虚高误触 R2、兄弟互斥误触 R1）
            var parentChain = paramter.Chain;

            foreach (var eventFlow in candidates)
            {
                if (!IsRunnableEventFlow(eventFlow, paramter))
                {
                    continue;
                }

                // 链尾 EF 配 Specified：仅放行清单内的候选，未命中的静默跳过（与历史行为一致）
                if (cascadeAllowedIds != null && !cascadeAllowedIds.Contains(eventFlow.Id))
                {
                    continue;
                }

                var chain = parentChain;
                // R1 循环中断：同一链上已执行过该 EventFlow（自环/链环），静默跳过并留痕
                // R2 深度上限：超限中断整链。只写日志告警，不置 Error、不抛异常——不连坐父审批/表单写入
                var (chainBlockReason, depthExceeded) = EvaluateChainBlock(chain, eventFlow.Id, MaxChainDepth);
                if (chainBlockReason != null)
                {
                    await WriteChainInterruptedLogAsync(eventFlow, paramter, chainBlockReason);
                    if (depthExceeded)
                    {
                        Logger.LogError("EventFlow 链深度超限，已中断整链。EventFlowId={EventFlowId}, ExecutionId={ExecutionId}, {Reason}",
                            eventFlow.Id, paramter.ExecutionId, chainBlockReason);
                    }
                    continue;
                }

                // R3 链上标记：深度+1、路径追加，随 EfDataContext 向下游传播
                paramter.WithChain(BuildChainForCandidate(chain, eventFlow.Id));

                var result = await RunSingleAsync(eventFlow, paramter);
                execResult.EfInstance = result.EfInstance ?? execResult.EfInstance;

                if (!result.Success)
                {
                    execResult.Error = string.IsNullOrEmpty(execResult.Error)
                        ? result.Error
                        : $"{execResult.Error}; {result.Error}";
                }
            }

            return execResult;
        }

        /// <summary>
        /// 解析出站级联门禁（链尾解析）：链尾 = 当前正在执行的 EF，其定义上的
        /// CascadeMode/SpecifiedEvents 决定本次数据变更还能触发哪些下游——每跳从各自定义现读。
        /// 链为空 = 入口触发，不设门禁。
        /// </summary>
        private async Task<(string? BlockReason, HashSet<string>? AllowedIds)> ResolveChainCascadeAsync(EfRunParameter paramter)
        {
            var chain = paramter.Chain;
            if (chain == null || chain.Path.Count == 0)
            {
                return (null, null);
            }

            var tailEventFlowId = chain.Path[^1];
            var repository = _resolver.Resolve<IRepository<Wf_Definition>>();
            var tailDefinition = repository.Find(x => x.Id == tailEventFlowId).FirstOrDefault();
            if (tailDefinition == null || tailDefinition.DeleteFlag || tailDefinition.Disabled)
            {
                // 链尾定义已删除/禁用：出站策略不可知，按最严格（Never）切断并留痕——兜底不依赖业务配置
                Logger.LogError("EventFlow 链尾定义已删除或禁用，级联下游已切断。EventFlowId={EventFlowId}, ExecutionId={ExecutionId}",
                    tailEventFlowId, paramter.ExecutionId);
                return ("ChainCascadeDefinitionMissing", null);
            }

            return EvaluateCascadeGate(tailDefinition.EventSetting);
        }

        /// <summary>
        /// 出站门禁纯函数：Never / Specified 空清单 → 整链阻断；Specified 非空 → 仅放行清单；
        /// All 或缺省（无 EventSetting）→ 不设门禁，与全链「缺省即 All」一致。
        /// </summary>
        private static (string? BlockReason, HashSet<string>? AllowedIds) EvaluateCascadeGate(EventSetting? setting)
        {
            var mode = setting?.CascadeMode ?? CascadeMode.All;
            if (mode == CascadeMode.Never)
            {
                return ("ChainCascadeNever", null);
            }

            if (mode == CascadeMode.Specified)
            {
                var specified = setting?.SpecifiedEvents;
                if (string.IsNullOrWhiteSpace(specified))
                {
                    return ("ChainCascadeSpecifiedEmpty", null);
                }

                var allowedIds = specified
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                return (null, allowedIds);
            }

            return (null, null);
        }

        /// <summary>
        /// 链规则判定（纯函数）：返回非空原因即中断该候选。
        /// R1 环检测（flowId 已在 Path，大小写不敏感）优先于 R2 深度上限。
        /// </summary>
        private static (string? Reason, bool DepthExceeded) EvaluateChainBlock(EfChainContext? chain, string eventFlowId, int maxChainDepth)
        {
            if (chain == null)
            {
                return (null, false);
            }

            if (chain.Path.Contains(eventFlowId, StringComparer.OrdinalIgnoreCase))
            {
                return ($"ChainLoopSuppressed, depth={chain.Depth}, path={FormatChainPath(chain.Path)}", false);
            }

            if (chain.Depth >= maxChainDepth)
            {
                return ($"ChainDepthExceeded, depth={chain.Depth + 1}/{maxChainDepth}, path={FormatChainPath(chain.Path)}", true);
            }

            return (null, false);
        }

        /// <summary>
        /// 由父链派生候选的执行链（纯函数）：深度父+1、路径追加自身；父链为 null 时开新链。
        /// 同批候选各自从同一父链派生，互不引用——保证 R1/R2 只对真实祖先链生效。
        /// </summary>
        private static EfChainContext BuildChainForCandidate(EfChainContext? parentChain, string eventFlowId)
        {
            return new EfChainContext
            {
                ChainId = parentChain?.ChainId ?? TsidIdGenerator.NewId(),
                Depth = (parentChain?.Depth ?? 0) + 1,
                Path = parentChain == null ? [eventFlowId] : [.. parentChain.Path, eventFlowId]
            };
        }

        private static List<Wf_Definition> LoadCandidates(IRepository<Wf_Definition> repository, EfRunParameter paramter)
        {
            var candidates = repository.Find(x => x.CorpId == paramter.Data.CorpId
                && x.FlowType == FlowType.EventFlow
                && !x.DeleteFlag
                && !x.Disabled
                && x.EventSetting != null
                && x.EventSource == paramter.EventSource).ToList();

            if (!string.IsNullOrEmpty(paramter.EventFlowId))
            {
                candidates = candidates.Where(x => x.Id == paramter.EventFlowId).ToList();
            }

            return candidates;
        }

        private static string FormatChainPath(List<string> path)
        {
            return string.Join(" -> ", path);
        }

        private async Task WriteCascadeBlockedLogsAsync(EfRunParameter paramter, string reason)
        {
            var repository = _resolver.Resolve<IRepository<Wf_Definition>>();
            //只给「除级联门禁外本就满足触发条件」的候选留痕：SourceId/EventType/WfNodeId/ChangeFields
            //全不匹配的 EF 与本次触发无关，不写日志污染 RunLog
            foreach (var eventFlow in LoadCandidates(repository, paramter).Where(x => IsRunnableEventFlow(x, paramter)))
            {
                await WriteChainInterruptedLogAsync(eventFlow, paramter, reason);
            }
        }

        private async Task WriteChainInterruptedLogAsync(Wf_Definition eventFlow, EfRunParameter paramter, string reason)
        {
            try
            {
                var runLogRepository = _resolver.Resolve<IRepository<Ef_RunLog>>();
                var runLog = CreateRunLog(eventFlow, paramter, DateTime.UtcNow.ToTimeStampMs());
                runLogRepository.Insert(runLog);
                UpdateRunLog(runLogRepository, runLog, string.Empty, false, reason);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "写入链中断运行日志失败。EventFlowId={EventFlowId}", eventFlow.Id);
            }
        }

        private bool IsRunnableEventFlow(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            if (!IsSourceMatched(eventFlow, paramter))
            {
                return false;
            }

            if (!IsEventMatched(eventFlow, paramter))
            {
                return false;
            }

            var setting = eventFlow.EventSetting!;
            if (!string.IsNullOrEmpty(setting.WfNodeId)
                && !string.Equals(setting.WfNodeId, paramter.WfNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(setting.NodeAction)
                && !string.Equals(setting.NodeAction, paramter.NodeAction, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!IsChangeFieldsMatched(eventFlow, paramter))
            {
                return false;
            }

            return IsMeet(eventFlow, paramter.Data);
        }

        private static bool IsSourceMatched(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            return string.IsNullOrEmpty(eventFlow.SourceId)
                || string.Equals(paramter.Data.FormId, eventFlow.SourceId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEventMatched(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            var configured = eventFlow.EventSetting!.EventType;
            return paramter.EventType == EventType.None
                ? configured == EventType.None
                : configured.HasFlag(paramter.EventType);
        }

        private static bool IsChangeFieldsMatched(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            if (paramter.EventType != EventType.Modified)
            {
                return true;
            }

            var configured = eventFlow.Metadata.Steps
                .FirstOrDefault()?
                .EfNodeSetting?
                .TriggerSetting?
                .ChangeFields;

            if (configured == null || configured.Count == 0)
            {
                return true;
            }

            if (paramter.ChangeFields == null || paramter.ChangeFields.Count == 0)
            {
                return false;
            }

            return configured
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Intersect(paramter.ChangeFields, StringComparer.OrdinalIgnoreCase)
                .Any();
        }

        private async Task<EfExecResult> RunSingleAsync(Wf_Definition eventFlow, EfRunParameter paramter)
        {
            var execResult = new EfExecResult();
            var runLogRepository = _resolver.Resolve<IRepository<Ef_RunLog>>();
            var startTime = DateTime.UtcNow.ToTimeStampMs();
            var runLog = CreateRunLog(eventFlow, paramter, startTime);
            runLog.StartTime = startTime;
            runLog.EndTime = null;
            runLog.Success = false;
            runLog.ErrMsg = string.Empty;
            try
            {
                runLog.RetryContextJson = new EfRunRetryContext
                {
                    ExecutionId = paramter.ExecutionId,
                    DataId = paramter.Data.Id,
                    WfNodeId = paramter.WfNodeId,
                    NodeAction = paramter.NodeAction,
                    ChangeFields = paramter.ChangeFields?.ToList(),
                    ChainJson = paramter.Chain?.SerializeToJson()
                }.SerializeToJson();
                runLogRepository.Insert(runLog);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "写入数据流运行日志失败。EventFlowId={EventFlowId}", eventFlow.Id);
                runLog = null;
            }

            var ctx = new EfDataContext()
            {
                CorpId = paramter.Data.CorpId ?? "",
                UserId = paramter.UserId,
                AccessToken = paramter.AccessToken,
                AppId = paramter.Data.AppId,
                EventFlowId = eventFlow.Id,
                RunLogId = runLog?.Id ?? string.Empty,
                ExecutionId = string.IsNullOrWhiteSpace(paramter.ExecutionId)
                    ? runLog?.Id ?? TsidIdGenerator.NewId()
                    : paramter.ExecutionId,
                WfNodeId = paramter.WfNodeId,
                NodeAction = paramter.NodeAction ?? string.Empty,
                FormId = paramter.Data.FormId,
                DataId = paramter.Data.Id,
                TriggerData = paramter.Data,
                WfStarter = paramter.Starter,
                Chain = paramter.Chain
            };

            try
            {
                // 重试需要从原 WorkflowInstance 恢复失败指针，因此 EF 运行必须持久化 WorkflowCore 状态。
                var efInst = await SyncWfRunner.RunWorkflowSync(eventFlow.ExternalId, 1, ctx, "", CancellationToken.None, true);
                var efDataContext = efInst.Data as EfDataContext;
                execResult.EfInstance = efInst;
                execResult.Error = efDataContext?.ErrMsg;
                UpdateRunLog(runLogRepository, runLog, efInst.Id, string.IsNullOrEmpty(execResult.Error), execResult.Error);
            }
            catch (Exception ex)
            {
                execResult.Error = ex.Message;
                UpdateRunLog(runLogRepository, runLog, string.Empty, false, ex.Message);
            }

            return execResult;
        }

        private static Ef_RunLog CreateRunLog(Wf_Definition eventFlow, EfRunParameter paramter, long startTime)
        {
            var triggerSetting = eventFlow.Metadata.Steps.FirstOrDefault()?.EfNodeSetting?.TriggerSetting;
            return new Ef_RunLog
            {
                Id = string.Empty,
                CorpId = paramter.Data.CorpId,
                AppId = eventFlow.AppId,
                EventFlowId = eventFlow.Id,
                EventFlowName = eventFlow.Name,
                EventFlowVersion = eventFlow.Version,
                TriggerKind = triggerSetting?.TriggerKind ?? GuessTriggerKind(paramter.EventSource),
                EventSource = paramter.EventSource,
                EventType = paramter.EventType,
                TriggerBy = paramter.Starter ?? Operator.Empty,
                TriggerTime = startTime,
                StartTime = startTime,
                Success = false,
                CreateBy = paramter.Starter ?? Operator.Empty,
                CreateTime = startTime,
            };
        }

        private void UpdateRunLog(IRepository<Ef_RunLog> repository, Ef_RunLog? runLog, string wfInstanceId, bool success, string? errMsg)
        {
            if (runLog == null)
            {
                return;
            }

            try
            {
                var endTime = DateTime.UtcNow.ToTimeStampMs();
                if (!string.IsNullOrEmpty(wfInstanceId))
                {
                    runLog.WfInstanceId = wfInstanceId;
                }

                runLog.EndTime = endTime;
                runLog.Success = success;
                runLog.ErrMsg = errMsg ?? string.Empty;
                runLog.UpdateTime = endTime;
                repository.Replace(runLog);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "更新数据流运行日志失败。RunLogId={RunLogId}", runLog.Id);
            }
        }

        private static EventFlowTriggerKind GuessTriggerKind(EventSourceType eventSource)
        {
            return eventSource switch
            {
                EventSourceType.Schedule => EventFlowTriggerKind.Schedule,
                EventSourceType.Http => EventFlowTriggerKind.Http,
                _ => EventFlowTriggerKind.Form,
            };
        }
    }
}
