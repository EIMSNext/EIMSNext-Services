using System.Dynamic;
using System.Text.Json;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Core;
using EIMSNext.Core.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Scripting;
using EIMSNext.Component;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public static class FormNotifyRuntime
    {
        public static async Task PublishToChannelsAsync(IMessagePublisher publisher, string corpId, string notifyId, string title, string detail, string url, long expireTime, MessageCategory category, NotifyChannel channels, List<NotifyReceiver> receivers, MessageType messageType, CancellationToken ct)
        {
            if (channels.HasFlag(NotifyChannel.System))
            {
                await publisher.PublishAsync(new SystemMessageTaskArgs
                {
                    CorpId = corpId,
                    NotifyId = notifyId,
                    Title = title,
                    Detail = detail,
                    Url = url,
                    ExpireTime = expireTime,
                    Category = category,
                    Receivers = receivers,
                    MessageType = messageType
                }, ct);
            }

            if (channels.HasFlag(NotifyChannel.Email))
            {
                await publisher.PublishAsync(new EmailNotifyTaskArgs
                {
                    CorpId = corpId,
                    NotifyId = notifyId,
                    Title = title,
                    Detail = detail,
                    Url = url,
                    Receivers = receivers,
                    MessageType = messageType
                }, ct);
            }
        }

        public static bool ShouldNotify(IResolver resolver, FormNotify notify, NotifyDispatchTaskArgs args)
        {
            if (args.NewData == null || !args.FormTriggerMode.HasValue)
            {
                return false;
            }

            if (args.FormTriggerMode == FormNotifyTriggerMode.DataChanged && args.OldData != null)
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

        public static long? ExtractTimeFieldValue(FormData data, string timeField)
        {
            if (string.Equals(timeField, Common.Fields.CreateTime, StringComparison.OrdinalIgnoreCase))
            {
                return data.CreateTime;
            }

            if (string.Equals(timeField, Common.Fields.UpdateTime, StringComparison.OrdinalIgnoreCase))
            {
                return data.UpdateTime;
            }

            var dict = (IDictionary<string, object?>)data.Data;
            if (!dict.TryGetValue(timeField, out var rawValue) || rawValue == null)
            {
                return null;
            }

            if (rawValue is long longValue)
            {
                return longValue;
            }

            if (rawValue is int intValue)
            {
                return intValue;
            }

            if (rawValue is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var num))
                {
                    return num;
                }

                if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out var parsed))
                {
                    return parsed;
                }
            }

            return long.TryParse(rawValue.ToString(), out var result) ? result : null;
        }
    }
}
