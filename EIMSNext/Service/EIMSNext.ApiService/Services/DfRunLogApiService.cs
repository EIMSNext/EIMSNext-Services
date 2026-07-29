using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Common;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 数据流运行日志聚合服务。
    /// 负责一次运行（Df_RunLog）的列表/详情查询，详情中包含该次运行下所有节点执行记录（Df_RunLogNode）。
    /// </summary>
    public class DfRunLogApiService : ApiServiceBase
    {
        public DfRunLogApiService(IResolver resolver) : base(resolver)
        {
        }

        private IDfRunLogService RunLogService => Resolver.GetService<IDfRunLogService, Df_RunLog>();
        private IDfRunLogNodeService RunLogNodeService => Resolver.GetService<IDfRunLogNodeService, Df_RunLogNode>();

        public async Task<(long total, IReadOnlyList<Df_RunLog> items)> GetRunsAsync(
            string dataflowId,
            long? startTime,
            long? endTime,
            bool? success,
            int skip,
            int top)
        {
            EnsureCanManageDataflow(dataflowId);

            var fb = RunLogService.Collection;
            var filterBuilder = Builders<Df_RunLog>.Filter;
            var filter = filterBuilder.Eq(x => x.CorpId, IdentityContext.CurrentCorpId)
                & filterBuilder.Eq(x => x.DataflowId, dataflowId)
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

        public async Task<DfRunLogDetail?> GetRunDetailAsync(string runLogId)
        {
            var run = await RunLogService.GetAsync(runLogId);
            if (run == null || run.CorpId != IdentityContext.CurrentCorpId || run.DeleteFlag)
            {
                return null;
            }

            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(run.AppId);

            var fb = RunLogNodeService.Collection;
            var nodes = await fb
                .Find(Builders<Df_RunLogNode>.Filter.And(
                    Builders<Df_RunLogNode>.Filter.Eq(x => x.RunLogId, runLogId),
                    Builders<Df_RunLogNode>.Filter.Eq(x => x.CorpId, IdentityContext.CurrentCorpId)))
                .SortBy(x => x.StartTime)
                .ToListAsync();

            return new DfRunLogDetail
            {
                Run = run,
                Nodes = nodes,
                ExecutedNodeIds = nodes.Select(x => x.NodeId).Distinct().ToList(),
                FailedNodeIds = nodes.Where(x => !x.Success).Select(x => x.NodeId).Distinct().ToList(),
            };
        }

        private void EnsureCanManageDataflow(string dataflowId)
        {
            var definition = Resolver.GetService<Wf_Definition>()
                .Query(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.Id == dataflowId &&
                    x.FlowType == FlowType.Dataflow)
                .FirstOrDefault();

            if (definition == null)
            {
                throw new BadRequestException("智能助手不存在");
            }

            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(definition.AppId);
        }
    }

    public class DfRunLogDetail
    {
        public Df_RunLog Run { get; set; } = default!;
        public IReadOnlyList<Df_RunLogNode> Nodes { get; set; } = Array.Empty<Df_RunLogNode>();
        public IReadOnlyList<string> ExecutedNodeIds { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> FailedNodeIds { get; set; } = Array.Empty<string>();
    }
}
