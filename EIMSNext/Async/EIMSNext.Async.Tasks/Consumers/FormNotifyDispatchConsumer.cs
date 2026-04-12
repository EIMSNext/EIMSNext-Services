using System.Dynamic;
using System.Text.Json;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Scripting;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace EIMSNext.Async.Tasks.Consumers
{
    public class FormNotifyDispatchConsumer : TaskConsumerBase<FormNotifyDispatchTaskArgs, FormNotifyDispatchConsumer>
    {
        public FormNotifyDispatchConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }

        protected override async Task HandleAsync(FormNotifyDispatchTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            var notifyRepo = resolver.GetRepository<FormNotify>();
            var formDataRepo = resolver.GetRepository<FormData>();
            var formDefRepo = resolver.GetRepository<FormDef>();
            var publisher = resolver.Resolve<IMessagePublisher>();
            var detailBuilder = resolver.Resolve<IFormNotifyDetailBuilder>();
            var recipientResolver = resolver.Resolve<IFormNotifyRecipientResolver>();

            var formDef = formDefRepo.Get(args.NewData.FormId);
            if (formDef == null)
            {
                return;
            }

            var notifies = notifyRepo.Find(x =>
                x.CorpId == args.CorpId &&
                x.AppId == args.NewData.AppId &&
                x.FormId == args.NewData.FormId &&
                !x.Disabled &&
                x.TriggerMode == args.TriggerMode).ToList();

            foreach (var notify in notifies)
            {
                if (!ShouldNotify(resolver, notify, args))
                {
                    continue;
                }

                var receivers = await recipientResolver.ResolveAsync(args.NewData, formDef, notify.Notifiers, args.Operator?.Id);
                if (receivers.Count == 0)
                {
                    continue;
                }

                var detail = args.TriggerMode == FormNotifyTriggerMode.DataAdded || args.OldData == null
                    ? detailBuilder.BuildForAdd(args.NewData, formDef)
                    : detailBuilder.BuildForChange(args.OldData, args.NewData, formDef);

                var title = notify.NotifyText ?? string.Empty;
                var url = $"/data/{args.DataId}";
                var expireTime = DateTime.UtcNow.AddDays(7).ToTimeStampMs();
                var channels = (FormNotifyChannel)notify.Channels;

                if (channels.HasFlag(FormNotifyChannel.System))
                {
                    await publisher.PublishAsync(new SystemMessageTaskArgs
                    {
                        CorpId = args.CorpId,
                        NotifyId = notify.Id,
                        Title = title,
                        Detail = detail,
                        Url = url,
                        ExpireTime = expireTime,
                        Category = MessageCategory.DataNotify,
                        Receivers = receivers
                    }, ct);
                }

                if (channels.HasFlag(FormNotifyChannel.Email))
                {
                    await publisher.PublishAsync(new EmailNotifyTaskArgs
                    {
                        CorpId = args.CorpId,
                        NotifyId = notify.Id,
                        Title = title,
                        Detail = detail,
                        Url = url,
                        Receivers = receivers
                    }, ct);
                }
            }
        }

        private static bool ShouldNotify(IResolver resolver, FormNotify notify, FormNotifyDispatchTaskArgs args)
        {
            if (args.TriggerMode == FormNotifyTriggerMode.DataChanged && args.OldData != null)
            {
                var changedFields = ExpandoComparer.Compare(args.OldData.Data, args.NewData.Data)
                    .Select(x => x.FieldId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (notify.ChangeFields?.Count > 0 && !notify.ChangeFields.Any(changedFields.Contains))
                {
                    return false;
                }
            }

            if (notify.TriggerMode == FormNotifyTriggerMode.DataAdded || notify.TriggerMode == FormNotifyTriggerMode.DataChanged)
            {
                if (!string.IsNullOrEmpty(notify.DataExpressFilter))
                {
                    var pData = args.NewData.Data;
                    pData.TryAdd("createBy", args.NewData.CreateBy);
                    return resolver.Resolve<IScriptEngine>().Evaluate<bool>(notify.DataExpressFilter, new Dictionary<string, object>()
                    {
                        ["data"] = pData
                    }).Value;
                }

                return true;
            }
            else
            {
                // TODO: 对于非新增/修改的，有可能是运算一个结果，有可能是取合条件的数据
                if (string.IsNullOrWhiteSpace(notify.DataDynamicFilter))
                {
                    return true;
                }

                var filter = notify.DataDynamicFilter.DeserializeFromJson<DynamicFilter>();
                if (filter == null)
                {
                    return true;
                }

                var composed = new DynamicFilter
                {
                    Rel = FilterRel.And,
                    Items =
                    [
                        new DynamicFilter { Field = nameof(FormData.Id), Op = FilterOp.Eq, Value = args.DataId },
                        filter
                    ]
                };

                return resolver.GetRepository<FormData>().Count(composed) > 0;
            }
        }
    }
}
