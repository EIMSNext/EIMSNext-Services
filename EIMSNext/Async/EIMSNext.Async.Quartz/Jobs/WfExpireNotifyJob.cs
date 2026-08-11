using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Flow.Persistence;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;
using Quartz;
using WorkflowCore.Models;

namespace EIMSNext.Async.Quartz.Jobs
{
    [DisallowConcurrentExecution]
    public class WfExpireNotifyJob : JobBase<WfExpireNotifyJob>, IWfExpireNotifyJob
    {
        public WfExpireNotifyJob(IResolver resolver) : base(resolver)
        {
        }

        protected override async Task ExecuteAsync(IJobExecutionContext context)
        {
            var taskRepo = Resolver.GetRepository<Wf_Task>();
            var wfDefRepo = Resolver.GetRepository<Wf_Definition>();
            var publisher = Resolver.Resolve<IMessagePublisher>();
            var now = DateTime.UtcNow.ToTimeStampMs();
            var expiredTasks = taskRepo.Find(x => !x.ExpireHandled && x.ExpireTime.HasValue && x.ExpireTime <= now).ToList();
            if (expiredTasks.Count == 0)
            {
                return;
            }

            var workflowCollection = Resolver.Resolve<IWfDbContext>().WorkflowInstances;
            foreach (var group in expiredTasks.GroupBy(x => new { x.WfInstanceId, x.ApproveNodeId }))
            {
                var sample = group.First();
                var workflow = workflowCollection.Find(x => x.Id == sample.WfInstanceId).FirstOrDefault();
                if (workflow == null)
                {
                    continue;
                }

                var definition = wfDefRepo.Find(x => x.ExternalId == workflow.WorkflowDefinitionId && x.Version == workflow.Version).FirstOrDefault();
                var step = definition?.Metadata?.Steps?.FirstOrDefault(x => x.Id == sample.ApproveNodeId);
                var expireSetting = step?.WfNodeSetting?.ApproveSetting?.ExpireSetting;
                if (expireSetting == null || expireSetting.TimeValue <= 0)
                {
                    await MarkExpireHandledAsync(taskRepo, group.Select(x => x.Id), now);
                    continue;
                }

                if (expireSetting.ActionType == WfExpireActionType.AutoNotify)
                {
                    await publisher.PublishAsync(new NotifyDispatchTaskArgs
                    {
                        CorpId = sample.CorpId ?? string.Empty,
                        MessageType = MessageType.WfExpireNotify,
                        AppId = sample.AppId,
                        FormId = sample.FormId,
                        DataId = sample.DataId,
                        WfInstanceId = sample.WfInstanceId,
                        ApproveNodeId = sample.ApproveNodeId
                    });

                    // The notification task is now durably queued. Mark the source tasks
                    // handled here so the minute-level scan does not publish duplicates.
                    await MarkExpireHandledAsync(taskRepo, group.Select(x => x.Id), now);
                }
                else
                {
                    await publisher.PublishAsync(new WorkflowExpireTaskArgs
                    {
                        CorpId = sample.CorpId ?? string.Empty,
                        WfInstanceId = sample.WfInstanceId,
                        DataId = sample.DataId,
                        WfNodeId = sample.ApproveNodeId,
                        TaskIds = group.Select(x => x.Id).Distinct().ToList(),
                        ActionType = expireSetting.ActionType
                    });
                }
            }
        }

        private static Task MarkExpireHandledAsync(IRepository<Wf_Task> taskRepo, IEnumerable<string> ids, long now)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0)
            {
                return Task.CompletedTask;
            }

            taskRepo.UpdateMany(Builders<Wf_Task>.Filter.In(x => x.Id, idList), Builders<Wf_Task>.Update
                .Set(x => x.ExpireHandled, true)
                .Set(x => x.UpdateTime, now), upsert: false);
            return Task.CompletedTask;
        }
    }
}
