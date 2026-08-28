using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Common;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 数据流运行日志聚合服务。
    /// 负责一次运行（Ef_RunLog）的列表/详情查询，详情中包含该次运行下所有节点执行记录（Ef_RunLogNode）。
    /// </summary>
    public class EfRunLogApiService : ApiServiceBase
    {
        public EfRunLogApiService(IResolver resolver) : base(resolver)
        {
        }

        private IEfRunLogService RunLogService => Resolver.GetService<IEfRunLogService, Ef_RunLog>();
        private IEfRunLogNodeService RunLogNodeService => Resolver.GetService<IEfRunLogNodeService, Ef_RunLogNode>();

        public async Task<(long total, IReadOnlyList<Ef_RunLog> items)> GetRunsAsync(
            string eventFlowId,
            long? startTime,
            long? endTime,
            bool? success,
            int skip,
            int top)
        {
            EnsureCanManageEventFlow(eventFlowId);

            var fb = RunLogService.Collection;
            var filterBuilder = Builders<Ef_RunLog>.Filter;
            var filter = filterBuilder.Eq(x => x.CorpId, IdentityContext.CurrentCorpId)
                & filterBuilder.Eq(x => x.EventFlowId, eventFlowId)
                & filterBuilder.Eq(x => x.DeleteFlag, false);

            if (startTime.HasValue)
            {
                filter &= filterBuilder.Gte(x => x.TriggerTime, startTime.Value);
            }

            if (endTime.HasValue)
            {
                filter &= filterBuilder.Lte(x => x.TriggerTime, endTime.Value);
            }

            if (success.HasValue)
            {
                filter &= filterBuilder.Eq(x => x.Success, success.Value);
            }

            var total = await RunLogService.Collection.CountDocumentsAsync(filter);
            var items = await RunLogService.Collection
                .Find(filter)
                .SortByDescending(x => x.TriggerTime)
                .Skip(skip)
                .Limit(top)
                .ToListAsync();

            return (total, items);
        }

        public async Task<EfRunLogDetail?> GetRunDetailAsync(string runLogId)
        {
            var run = await RunLogService.GetAsync(runLogId);
            if (run == null || run.CorpId != IdentityContext.CurrentCorpId || run.DeleteFlag)
            {
                return null;
            }

            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(run.AppId);

            var fb = RunLogNodeService.Collection;
            var nodes = await fb
                .Find(Builders<Ef_RunLogNode>.Filter.And(
                    Builders<Ef_RunLogNode>.Filter.Eq(x => x.RunLogId, runLogId),
                    Builders<Ef_RunLogNode>.Filter.Eq(x => x.CorpId, IdentityContext.CurrentCorpId)))
                .SortBy(x => x.StartTime)
                .ToListAsync();

            return new EfRunLogDetail
            {
                Run = run,
                Nodes = nodes,
                ExecutedNodeIds = nodes.Select(x => x.NodeId).Distinct().ToList(),
                FailedNodeIds = nodes.Where(x => !x.Success).Select(x => x.NodeId).Distinct().ToList(),
            };
        }

        private void EnsureCanManageEventFlow(string eventFlowId)
        {
            var definition = Resolver.GetService<Wf_Definition>()
                .Query(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.Id == eventFlowId &&
                    x.FlowType == FlowType.EventFlow)
                .FirstOrDefault();

            if (definition == null)
            {
                throw new BadRequestException("智能助手不存在");
            }

            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(definition.AppId);
        }
    }

    public class EfRunLogDetail
    {
        public Ef_RunLog Run { get; set; } = default!;
        public IReadOnlyList<Ef_RunLogNode> Nodes { get; set; } = Array.Empty<Ef_RunLogNode>();
        public IReadOnlyList<string> ExecutedNodeIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> FailedNodeIds { get; set; } = Array.Empty<string>();
    }
}
