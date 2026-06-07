using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Component;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.MongoDb;
using EIMSNext.Service;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Async.Tasks.Consumers
{
    public class NotifyDispatchConsumer : TaskConsumerBase<NotifyDispatchTaskArgs, NotifyDispatchConsumer>
    {
        public NotifyDispatchConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }

        protected override Task HandleAsync(NotifyDispatchTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            return args.MessageType switch
            {
                MessageType.FormNotify => HandleFormNotifyAsync(args, ct, resolver),
                MessageType.WfTodoNotify => HandleWfTodoNotifyAsync(args, ct, resolver),
                MessageType.WfExpireNotify => HandleWfExpireNotifyAsync(args, ct, resolver),
                MessageType.WfUrgeNotify => HandleWfUrgeNotifyAsync(args, ct, resolver),
                _ => Task.CompletedTask
            };
        }

        private static async Task HandleFormNotifyAsync(NotifyDispatchTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (args.NewData == null || !args.FormTriggerMode.HasValue)
            {
                return;
            }

            var notifyRepo = resolver.GetRepository<FormNotify>();
            var formDefRepo = resolver.GetRepository<FormDef>();
            var formDataRepo = resolver.GetRepository<FormData>();
            var publisher = resolver.Resolve<IMessagePublisher>();
            var detailBuilder = resolver.Resolve<IFormNotifyDetailBuilder>();
            var recipientResolver = resolver.Resolve<IFormNotifyRecipientResolver>();
            var templateResolver = resolver.Resolve<DataTitleResolver>();

            var currentData = args.NewData;
            if (args.FormTriggerMode == FormNotifyTriggerMode.TimeFieldScheduled && !string.IsNullOrWhiteSpace(args.DataId))
            {
                currentData = formDataRepo.Get(args.DataId);
                if (currentData == null)
                {
                    return;
                }
                args.NewData = currentData;
            }

            var formDef = formDefRepo.Get(currentData.FormId);
            if (formDef == null)
            {
                return;
            }

            var notifies = notifyRepo.Find(x =>
                x.CorpId == args.CorpId &&
                x.AppId == currentData.AppId &&
                x.FormId == currentData.FormId &&
                !x.Disabled &&
                x.TriggerMode == args.FormTriggerMode.Value).ToList();

            foreach (var notify in notifies)
            {
                if (!FormNotifyRuntime.ShouldNotify(resolver, notify, args))
                {
                    continue;
                }

                var receivers = await recipientResolver.ResolveAsync(currentData, formDef, notify.Notifiers, args.Operator?.Id);
                if (receivers.Count == 0)
                {
                    continue;
                }

                var detail = args.FormTriggerMode == FormNotifyTriggerMode.DataAdded || args.OldData == null
                    ? detailBuilder.BuildForAdd(currentData, formDef)
                    : detailBuilder.BuildForChange(args.OldData, currentData, formDef);

                var title = templateResolver.Resolve(notify.NotifyText, currentData, formDef);
                var url = args.FormTriggerMode == FormNotifyTriggerMode.CustomScheduled
                    ? $"/app/{notify.AppId}/form/{notify.FormId}"
                    : $"/app/{currentData.AppId}/form/{currentData.FormId}/data/{args.DataId}";
                var expireTime = DateTime.UtcNow.AddDays(7).ToTimeStampMs();
                var channels = (NotifyChannel)notify.Channels;

                await FormNotifyRuntime.PublishToChannelsAsync(publisher, args.CorpId, notify.Id, title, detail, url, expireTime, MessageCategory.DataNotify, channels, receivers, args.MessageType, ct);
            }
        }

        private static async Task HandleWfTodoNotifyAsync(NotifyDispatchTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(args.WfInstanceId) || string.IsNullOrWhiteSpace(args.ApproveNodeId))
            {
                return;
            }

            var todoRepo = resolver.GetRepository<Wf_Todo>();
            var todos = todoRepo.Find(x => x.WfInstanceId == args.WfInstanceId && x.ApproveNodeId == args.ApproveNodeId).ToList();
            if (todos.Count == 0)
            {
                return;
            }

            var sample = todos[0];
            var step = GetWorkflowStep(resolver, sample.WfInstanceId, sample.ApproveNodeId);
            var channels = step?.WfNodeSetting?.ApproveSetting?.NotifyChannels ?? NotifyChannel.None;
            if (channels == NotifyChannel.None)
            {
                var definition = GetWorkflowDefinition(resolver, sample.WfInstanceId);
                channels = definition?.Metadata?.WorkflowSetting?.NotifyChannels ?? NotifyChannel.None;
            }
            if (channels == NotifyChannel.None)
            {
                return;
            }

            var receivers = await ResolveTodoReceiversAsync(resolver, todos.Select(x => x.EmployeeId));
            if (receivers.Count == 0)
            {
                return;
            }

            var title = $"你有一条新的审批待办：{sample.ApproveNodeName}";
            var detail = BuildTodoDetail(sample);
            var url = $"/workflow/todo/{sample.DataId}";
            var notifyId = $"{sample.WfInstanceId}:{sample.ApproveNodeId}:todo";
            var expireTime = DateTime.UtcNow.AddDays(7).ToTimeStampMs();
            var publisher = resolver.Resolve<IMessagePublisher>();

            await FormNotifyRuntime.PublishToChannelsAsync(publisher, sample.CorpId ?? string.Empty, notifyId, title, detail, url, expireTime, MessageCategory.FlowNotify, channels, receivers, args.MessageType, ct);
        }

        private static async Task HandleWfExpireNotifyAsync(NotifyDispatchTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(args.WfInstanceId) || string.IsNullOrWhiteSpace(args.ApproveNodeId))
            {
                return;
            }

            var todoRepo = resolver.GetRepository<Wf_Todo>();
            var todos = todoRepo.Find(x => x.WfInstanceId == args.WfInstanceId && x.ApproveNodeId == args.ApproveNodeId).ToList();
            if (todos.Count == 0)
            {
                return;
            }

            var sample = todos[0];
            var step = GetWorkflowStep(resolver, sample.WfInstanceId, sample.ApproveNodeId);
            var expireSetting = step?.WfNodeSetting?.ApproveSetting?.ExpireSetting;
            if (expireSetting?.ActionType != WfExpireActionType.AutoNotify)
            {
                return;
            }

            var notifySetting = expireSetting.NotifySetting;
            if (notifySetting == null || notifySetting.Channels == NotifyChannel.None || notifySetting.Candidates?.Count == 0)
            {
                return;
            }

            var formDefRepo = resolver.GetRepository<FormDef>();
            var formDataRepo = resolver.GetRepository<FormData>();
            var formDef = formDefRepo.Get(sample.FormId);
            var formData = formDataRepo.Get(sample.DataId);
            if (formDef == null || formData == null)
            {
                return;
            }

            var recipientResolver = resolver.Resolve<IFormNotifyRecipientResolver>();
            var receivers = await recipientResolver.ResolveCandidatesAsync(formData, formDef, notifySetting.Candidates ?? [], args.Operator?.Id);
            if (receivers.Count == 0)
            {
                return;
            }

            var title = $"审批待办已超时：{sample.ApproveNodeName}";
            var detail = BuildTodoDetail(sample);
            var url = $"/workflow/todo/{sample.DataId}";
            var notifyId = $"{sample.WfInstanceId}:{sample.ApproveNodeId}:expire";
            var expireTime = DateTime.UtcNow.AddDays(7).ToTimeStampMs();
            var publisher = resolver.Resolve<IMessagePublisher>();

            await FormNotifyRuntime.PublishToChannelsAsync(publisher, sample.CorpId ?? string.Empty, notifyId, title, detail, url, expireTime, MessageCategory.FlowNotify, notifySetting.Channels, receivers, args.MessageType, ct);
        }

        private static async Task HandleWfUrgeNotifyAsync(NotifyDispatchTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(args.WfInstanceId))
            {
                return;
            }

            var todoRepo = resolver.GetRepository<Wf_Todo>();
            var todos = todoRepo.Find(x => x.WfInstanceId == args.WfInstanceId).ToList();
            if (todos.Count == 0)
            {
                return;
            }

            var sample = todos[0];
            var definition = GetWorkflowDefinition(resolver, sample.WfInstanceId);
            var channels = definition?.Metadata?.WorkflowSetting?.NotifyChannels ?? NotifyChannel.System;
            if (channels == NotifyChannel.None)
            {
                channels = NotifyChannel.System;
            }

            var receivers = await ResolveTodoReceiversAsync(resolver, todos.Select(x => x.EmployeeId));
            if (receivers.Count == 0)
            {
                return;
            }

            var title = $"流程发起人催办：{sample.ApproveNodeName}";
            var detail = BuildTodoDetail(sample);
            var url = $"/workflow/todo/{sample.DataId}";
            var notifyId = $"{sample.WfInstanceId}:{sample.ApproveNodeId}:urge";
            var expireTime = DateTime.UtcNow.AddDays(7).ToTimeStampMs();
            var publisher = resolver.Resolve<IMessagePublisher>();

            await FormNotifyRuntime.PublishToChannelsAsync(publisher, sample.CorpId ?? string.Empty, notifyId, title, detail, url, expireTime, MessageCategory.FlowNotify, channels, receivers, args.MessageType, ct);
        }

        private static async Task<List<NotifyReceiver>> ResolveTodoReceiversAsync(IResolver resolver, IEnumerable<string> empIds)
        {
            var targetEmpIds = empIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (targetEmpIds.Count == 0)
            {
                return [];
            }

            var receivers = new List<NotifyReceiver>();
            await resolver.GetRepository<Employee>().Find(x => targetEmpIds.Contains(x.Id) && !x.IsDummy && x.Status == 0)
                .ForEachAsync(x => receivers.Add(new NotifyReceiver
                {
                    EmpId = x.Id,
                    EmpName = x.EmpName,
                    Email = x.WorkEmail
                }));

            return receivers;
        }

        private static WfStep? GetWorkflowStep(IResolver resolver, string wfInstanceId, string approveNodeId)
        {
            var definition = GetWorkflowDefinition(resolver, wfInstanceId);
            return definition?.Metadata?.Steps?.FirstOrDefault(x => x.Id == approveNodeId);
        }

        private static Wf_Definition? GetWorkflowDefinition(IResolver resolver, string wfInstanceId)
        {
            var raw = resolver.Resolve<IMongoDbContex>().Database.GetCollection<BsonDocument>("Wf_WorkflowInstance")
                .Find(Builders<BsonDocument>.Filter.Eq("Id", wfInstanceId))
                .FirstOrDefault();
            if (raw == null)
            {
                return null;
            }

            if (!raw.TryGetValue("WorkflowDefinitionId", out var workflowDefinitionIdValue) ||
                !raw.TryGetValue("Version", out var versionValue))
            {
                return null;
            }

            var workflowDefinitionId = workflowDefinitionIdValue.AsString;
            var version = versionValue.ToInt32();

            return resolver.GetRepository<Wf_Definition>()
                .Find(x => x.ExternalId == workflowDefinitionId && x.Version == version)
                .FirstOrDefault();
        }

        private static string BuildTodoDetail(Wf_Todo todo)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(todo.Starter?.Label))
            {
                lines.Add($"发起人: {todo.Starter.Label}");
            }

            foreach (var item in todo.DataBrief.Take(5))
            {
                if (item.Value == null || string.IsNullOrWhiteSpace(item.Value.ToString()))
                {
                    continue;
                }

                lines.Add($"{item.Title}: {item.Value}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
