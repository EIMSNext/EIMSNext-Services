using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.MongoDb;
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
            var todoRepo = Resolver.GetRepository<Wf_Todo>();
            var wfDefRepo = Resolver.GetRepository<Wf_Definition>();
            var publisher = Resolver.Resolve<IMessagePublisher>();
            var now = DateTime.UtcNow.ToTimeStampMs();
            var expiredTodos = todoRepo.Find(x => !x.ExpireHandled && x.ExpireTime.HasValue && x.ExpireTime <= now).ToList();
            if (expiredTodos.Count == 0)
            {
                return;
            }

            var workflowCollection = Resolver.Resolve<IMongoDbContex>().Database.GetCollection<WorkflowInstance>("Wf_WorkflowInstance");
            foreach (var group in expiredTodos.GroupBy(x => new { x.WfInstanceId, x.ApproveNodeId }))
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
                if (expireSetting?.ActionType != WfExpireActionType.AutoNotify)
                {
                    await MarkExpireHandledAsync(todoRepo, group.Select(x => x.Id), now);
                    continue;
                }

                await publisher.PublishAsync(new NotifyDispatchTaskArgs
                {
                    CorpId = sample.CorpId,
                    MessageType = MessageType.WfExpireNotify,
                    AppId = sample.AppId,
                    FormId = sample.FormId,
                    DataId = sample.DataId,
                    WfInstanceId = sample.WfInstanceId,
                    ApproveNodeId = sample.ApproveNodeId
                });

                await MarkExpireHandledAsync(todoRepo, group.Select(x => x.Id), now);
            }
        }

        private static Task MarkExpireHandledAsync(IRepository<Wf_Todo> todoRepo, IEnumerable<string> ids, long now)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0)
            {
                return Task.CompletedTask;
            }

            todoRepo.UpdateMany(Builders<Wf_Todo>.Filter.In(x => x.Id, idList), Builders<Wf_Todo>.Update
                .Set(x => x.ExpireHandled, true)
                .Set(x => x.UpdateTime, now), upsert: false);
            return Task.CompletedTask;
        }
    }
}
