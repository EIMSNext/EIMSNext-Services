using System.Dynamic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EIMSNext.ApiClient.Flow;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common;
using EIMSNext.Cache;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Common;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class FormDataService : EntityServiceBase<FormData>, IFormDataService
    {
        private FlowApiClient _flowClient;
        private ISerialNoSequenceService? _serialNoSvc;
        public FormDataService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
            _serialNoSvc = resolver.Resolve<ISerialNoSequenceService>();
        }

        protected override void CreateAuditLog(DbAction action, IEnumerable<FormData>? oldData, IEnumerable<FormData>? newData, FilterDefinition<FormData>? filter, UpdateDefinition<FormData>? update, IClientSessionHandle? session)
        {
            base.CreateAuditLog(action, oldData, newData, filter, update, session);

            // FormDataChangeLog 是 FormData 详情页右侧的业务变更记录，不在通用审计日志方法里写入。
            // if (oldData == null || !oldData.Any())
            // {
            //     // 新增不写 FormDataChangeLog
            // }
            // else if (newData == null || !newData.Any())
            // {
            //     // 删除不写 FormDataChangeLog
            // }
            // else
            // {
            //     // FormDataChangeLog 的更新记录已移动到 AfterReplace 中写入。
            //     // var changeLogs = ExpandoComparer.Compare(oldData.First().Data, newData.First().Data);
            // }
            //
            // var dataLog = new FormDataChangeLog();
            // TODO: 保存变更日志
            // switch (action)
            // {
            //     case DbAction.Insert:
            //         break;
            //     case DbAction.Update:
            //         break;
            //     default:
            //         break;
            // }
        }

        private static Dictionary<string, FieldDef> BuildFieldLookup(FormDef? formDef)
        {
            var lookup = new Dictionary<string, FieldDef>(StringComparer.OrdinalIgnoreCase);
            if (formDef?.Content?.Items == null) return lookup;

            foreach (var field in formDef.Content.Items)
            {
                AddField(field, null);
            }

            return lookup;

            void AddField(FieldDef field, string? parentField)
            {
                if (string.IsNullOrWhiteSpace(field.Field)) return;

                lookup.TryAdd(field.Field, field);

                if (!string.IsNullOrWhiteSpace(parentField))
                {
                    lookup.TryAdd($"{parentField}>{field.Field}", field);
                }

                if (field.Columns == null) return;
                foreach (var column in field.Columns)
                {
                    AddField(column, field.Field);
                }
            }
        }

        private static DataChangeContent ToDataChangeContent(ExpandoChangeLog changeLog, IReadOnlyDictionary<string, FieldDef> fieldLookup)
        {
            fieldLookup.TryGetValue(changeLog.FieldId, out var fieldDef);

            return new DataChangeContent
            {
                FieldId = changeLog.FieldId,
                FieldLabel = string.IsNullOrWhiteSpace(fieldDef?.Title) ? changeLog.FieldId : fieldDef.Title,
                FieldType = string.IsNullOrWhiteSpace(fieldDef?.Type) ? FieldType.Input : fieldDef.Type,
                ChangeType = changeLog.ChangeType,
                OriVallue = changeLog.OriValue,
                NewVallue = changeLog.NewValue
            };
        }

        protected override Task BeforeAdd(IEnumerable<FormData> entities, IClientSessionHandle? session)
        {
            var formDef = GetFromStore<FormDef>(entities.First().FormId)!;
            if (Context.Action == DataAction.Submit)
            {
                entities.ForEach(entity => ResolveSerialNumbers(entity, formDef, null));
            }
            if (!formDef.UsingWorkflow)
            {
                //非流程单据直接生效
                entities.ForEach(entity => { entity.FlowStatus = FlowStatus.Approved; });
            }
            return base.BeforeAdd(entities, session);
        }

        public override async Task AddAsync(IEnumerable<FormData> entities)
        {
            await base.AddAsync(entities);
            await SubmitAsync(entities, null, EIMSNext.Service.Entities.CascadeMode.NotSet, null);
        }

        protected override async Task AfterAdd(IEnumerable<FormData> entities, IClientSessionHandle? session)
        {
            var messagePublisher = Resolver.Resolve<IMessagePublisher>();
            var entity = entities.First();
            await EnqueueWebhookAsync(messagePublisher, entity, WebHookTrigger.Data_Created);

            await EnqueueFormNotify(messagePublisher, entity, null, FormNotifyTriggerMode.DataAdded);
            await RebuildTimeFieldNotifySchedulesAsync(entity, session);
            await base.AfterAdd(entities, session);
        }

        public override async Task<ReplaceOneResult> ReplaceAsync(FormData entity)
        {
            var result = await base.ReplaceAsync(entity);
            await SubmitAsync([entity], null, EIMSNext.Service.Entities.CascadeMode.NotSet, null);
            return result;
        }

        protected override async Task AfterReplace(FormData entity, IClientSessionHandle? session)
        {
            var messagePublisher = Resolver.Resolve<IMessagePublisher>();
            var old = ScopeCache.Get<FormData>(entity.Id, DataVersion.Old);
            var oriValue = new ExpandoObject();
            IList<ExpandoChangeLog> changeLogs = [];
            if (old != null)
            {
                changeLogs = ExpandoComparer.Compare(old.Data, entity.Data);
                changeLogs.ForEach(x => oriValue.TryAdd(x.FieldId, x.OriValue));
                CreateFormDataChangeLog(entity, changeLogs, session);
            }

            var formExp = entity.SerializeToJson().DeserializeFromJson<ExpandoObject>()!;
            formExp.TryAdd("oridata", oriValue);
            await EnqueueWebhookAsync(messagePublisher, entity, WebHookTrigger.Data_Updated, formExp);

            await EnqueueFormNotify(messagePublisher, entity, old, FormNotifyTriggerMode.DataChanged);
            await RebuildTimeFieldNotifySchedulesAsync(entity, session);

            await base.AfterReplace(entity, session);
        }

        private void CreateFormDataChangeLog(FormData entity, IList<ExpandoChangeLog> changeLogs, IClientSessionHandle? session)
        {
            if (changeLogs.Count == 0) return;

            var formDef = GetFromStore<FormDef>(entity.FormId);
            var fieldLookup = BuildFieldLookup(formDef);
            var content = changeLogs.Select(x => ToDataChangeContent(x, fieldLookup)).ToList();
            if (content.Count == 0) return;

            var now = DateTime.UtcNow.ToTimeStampMs();
            Resolver.GetRepository<FormDataChangeLog>().Insert(new FormDataChangeLog
            {
                CorpId = entity.CorpId ?? Context.CorpId,
                AppId = entity.AppId,
                FormId = entity.FormId,
                DataId = entity.Id,
                Operator = Context.Operator,
                OperateTime = now,
                Content = content,
                CreateBy = Context.Operator,
                CreateTime = now,
                UpdateBy = Context.Operator,
                UpdateTime = now
            }, session);
        }

        protected override Task BeforeReplace(FormData entity, IClientSessionHandle? session)
        {
            if (Context.Action == DataAction.Submit)
            {
                var formDef = GetFromStore<FormDef>(entity.FormId)!;
                var old = ScopeCache.Get<FormData>(entity.Id, DataVersion.Old);
                ResolveSerialNumbers(entity, formDef, old);
            }

            return base.BeforeReplace(entity, session);
        }

        public async Task SubmitAsync(IEnumerable<FormData> entities, IClientSessionHandle? session, EIMSNext.Service.Entities.CascadeMode cascade, string? eventIds)
        {
            var entity = entities.First();

            if (Context.Action == EIMSNext.Core.Entities.DataAction.Submit)
            {
                var formDef = GetFromStore<FormDef>(entity.FormId)!;

                if (formDef.UsingWorkflow)
                {
                    var wfDef = Resolver.GetRepository<Wf_Definition>().Find(x => x.ExternalId == entity.FormId).FirstOrDefault();
                    if (wfDef != null)
                    {
                        var wfResp = await _flowClient.Start(new StartRequest { WfDefinitionId = entity.FormId, DataId = entity.Id }, Context.AccessToken);
                        if (wfResp != null && !string.IsNullOrEmpty(wfResp.Error))
                        {
                            throw new UnLogException(wfResp.Error);
                        }
                    }
                }
                else
                {
                    if (cascade != EIMSNext.Service.Entities.CascadeMode.Never)
                    {
                        //非流程单据直接提交
                        var dfResp = await _flowClient.RunDataflow(new DfRunRequest { DataId = entity.Id, EventSource = ApiClient.Flow.EventSourceType.Form, EventType = ApiClient.Flow.EventType.Submit }, Context.AccessToken);
                        if (dfResp != null && !string.IsNullOrEmpty(dfResp.Error))
                        {
                            throw new UnLogException(dfResp.Error);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 解析表单数据中的所有 serialno 字段:
        /// - 当前值非空时保留,允许外部系统/API 预写流水号
        /// - 当前值为空但旧值非空时保留旧值
        /// - 当前值和旧值都为空时按规则生成
        /// </summary>
        private void ResolveSerialNumbers(FormData entity, FormDef formDef, FormData? oldEntity)
        {
            if (_serialNoSvc == null || formDef?.Content == null) return;
            var layout = formDef.Content.Layout;
            if (string.IsNullOrWhiteSpace(layout)) return;

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(layout);
            }
            catch
            {
                return;
            }

            var dataDict = (IDictionary<string, object?>)entity.Data!;
            var oldDataDict = oldEntity?.Data as IDictionary<string, object?>;
            WalkSerialNoRules(doc.RootElement, (rule) =>
            {
                if (!rule.TryGetProperty("field", out var fieldProp) || fieldProp.ValueKind != JsonValueKind.String) return;
                var field = fieldProp.GetString();
                if (string.IsNullOrEmpty(field)) return;

                if (dataDict.TryGetValue(field, out var currentValue)
                    && !string.IsNullOrWhiteSpace(currentValue?.ToString()))
                {
                    return;
                }

                if (oldDataDict != null
                    && oldDataDict.TryGetValue(field, out var oldValue)
                    && !string.IsNullOrWhiteSpace(oldValue?.ToString()))
                {
                    dataDict[field] = oldValue;
                    return;
                }

                if (!rule.TryGetProperty("props", out var propsEl) || propsEl.ValueKind != JsonValueKind.Object) return;
                if (!propsEl.TryGetProperty("segments", out var segmentsEl) || segmentsEl.ValueKind != JsonValueKind.Array) return;

                var sb = new StringBuilder();
                foreach (var seg in segmentsEl.EnumerateArray())
                {
                    AppendSegment(seg, sb, entity, dataDict, field);
                }
                dataDict[field] = sb.ToString();
            });

            doc.Dispose();
        }

        private void AppendSegment(JsonElement seg, StringBuilder sb, FormData entity, IDictionary<string, object?> dataDict, string serialNoField)
        {
            if (!seg.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) return;
            var type = typeEl.GetString();
            switch (type)
            {
                case "fixed":
                    {
                        var v = seg.TryGetProperty("value", out var ve) && ve.ValueKind == JsonValueKind.String ? ve.GetString() : null;
                        sb.Append(v ?? string.Empty);
                        break;
                    }
                case "date":
                    {
                        var fmt = seg.TryGetProperty("format", out var fe) && fe.ValueKind == JsonValueKind.String
                            ? fe.GetString()
                            : "yyyyMMdd";
                        sb.Append(DateTime.UtcNow.ToString(NormalizeDateFormat(fmt), CultureInfo.InvariantCulture));
                        break;
                    }
                case "field":
                    {
                        if (seg.TryGetProperty("field", out var fe) && fe.ValueKind == JsonValueKind.String)
                        {
                            var refField = fe.GetString();
                            if (!string.IsNullOrEmpty(refField) && dataDict.TryGetValue(refField, out var fv) && fv != null)
                            {
                                sb.Append(fv.ToString() ?? string.Empty);
                            }
                        }
                        break;
                    }
                case "counter":
                    {
                        var digits = seg.TryGetProperty("digits", out var de) && de.ValueKind == JsonValueKind.Number ? de.GetInt32() : 5;
                        var padZero = !(seg.TryGetProperty("padZero", out var pe) && pe.ValueKind == JsonValueKind.False);
                        var cycle = SerialNoResetCycle.Never;
                        if (seg.TryGetProperty("reset", out var re) && re.ValueKind == JsonValueKind.String)
                        {
                            cycle = re.GetString() switch
                            {
                                "day" => SerialNoResetCycle.Day,
                                "month" => SerialNoResetCycle.Month,
                                "year" => SerialNoResetCycle.Year,
                                _ => SerialNoResetCycle.Never
                            };
                        }
                        var seq = _serialNoSvc!.NextFormSerialNo(
                            entity.CorpId ?? string.Empty,
                            entity.AppId,
                            entity.FormId,
                            serialNoField,
                            cycle);
                        sb.Append(FormatCounter(seq, digits, padZero));
                        break;
                    }
            }
        }

        private static string FormatCounter(int seq, int digits, bool padZero)
        {
            if (!padZero || digits <= 0) return seq.ToString(CultureInfo.InvariantCulture);
            return seq.ToString("D" + Math.Min(digits, 10).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string NormalizeDateFormat(string? format)
        {
            if (string.IsNullOrWhiteSpace(format)) return "yyyyMMdd";
            return format.All(c => c is 'y' or 'M' or 'd' or '-' or '_' or '/' or '.') ? format : "yyyyMMdd";
        }

        private static void WalkSerialNoRules(JsonElement node, Action<JsonElement> visit)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                    && t.GetString() == FieldType.SerialNo)
                {
                    visit(node);
                    // 不递归 children,避免 tableform 内嵌的同名子规则被误处理
                    return;
                }
                foreach (var prop in node.EnumerateObject())
                {
                    if (prop.Name == "children" && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in prop.Value.EnumerateArray())
                            WalkSerialNoRules(child, visit);
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        WalkSerialNoRules(prop.Value, visit);
                    }
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                    WalkSerialNoRules(item, visit);
            }
        }

        public async Task<FilterOptionResult> GetFieldOptionsAsync(FilterOptionQuery query)
        {
            var rawValues = await Repository.DistinctFieldValuesAsync(query.Filter, query.FieldPath);
            var items = ProcessDistinctValues(rawValues, query.Keyword, query.Limit);
            return new FilterOptionResult { Items = items };
        }

        private static List<FilterOptionItem> ProcessDistinctValues(List<BsonValue> values, string? keyword, int limit)
        {
            var items = new List<FilterOptionItem>();
            foreach (var value in values)
            {
                if (value == null || value.IsBsonNull) continue;

                foreach (var option in ExpandOptionValues(value))
                {
                    if (!string.IsNullOrWhiteSpace(keyword) && option.Label?.Contains(keyword, StringComparison.OrdinalIgnoreCase) != true)
                        continue;

                    if (items.Any(x => x.Id == option.Id))
                        continue;

                    items.Add(option);
                    if (items.Count >= limit) break;
                }

                if (items.Count >= limit) break;
            }

            return items;
        }

        private static IEnumerable<FilterOptionItem> ExpandOptionValues(BsonValue value)
        {
            if (value.IsBsonArray)
            {
                foreach (var item in value.AsBsonArray)
                {
                    foreach (var option in ExpandOptionValues(item))
                        yield return option;
                }
                yield break;
            }

            if (value.IsBsonDocument)
            {
                var doc = value.AsBsonDocument;
                var id = doc.TryGetValue("id", out var idValue) ? idValue.ToString() : value.ToString();
                var label = doc.TryGetValue("label", out var labelValue)
                    ? labelValue.ToString()
                    : doc.TryGetValue("name", out var nameValue)
                        ? nameValue.ToString()
                        : id;

                yield return new FilterOptionItem
                {
                    Id = id,
                    Label = label,
                    Value = BsonTypeMapper.MapToDotNetValue(value)
                };
                yield break;
            }

            var scalar = BsonTypeMapper.MapToDotNetValue(value);
            var text = scalar?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return new FilterOptionItem
                {
                    Id = text,
                    Label = text,
                    Value = scalar
                };
            }
        }

        private Task EnqueueFormNotify(IMessagePublisher messagePublisher, FormData newData, FormData? oldData, FormNotifyTriggerMode triggerMode)
        {
            return messagePublisher.PublishAsync(new NotifyDispatchTaskArgs
            {
                CorpId = Context.CorpId,
                MessageType = MessageType.FormNotify,
                AppId = newData.AppId,
                FormId = newData.FormId,
                DataId = newData.Id,
                FormTriggerMode = triggerMode,
                Operator = Context.Operator,
                NewData = newData.SerializeToJson().DeserializeFromJson<FormData>()!,
                OldData = oldData?.SerializeToJson().DeserializeFromJson<FormData>()
            });
        }

        private static Task EnqueueWebhookAsync(IMessagePublisher messagePublisher, FormData entity, WebHookTrigger trigger, object? payload = null)
        {
            return messagePublisher.PublishAsync(new WebhookTaskArgs
            {
                CorpId = entity.CorpId ?? string.Empty,
                AppId = entity.AppId,
                FormId = entity.FormId,
                Trigger = trigger,
                PayloadJson = (payload ?? entity).SerializeToJson()
            });
        }

        private async Task RebuildTimeFieldNotifySchedulesAsync(FormData entity, IClientSessionHandle? session)
        {
            var notifyRepo = Resolver.GetRepository<FormNotify>();
            var scheduleRepo = Resolver.GetRepository<FormNotifyScheduleItem>();
            var formDef = GetFromStore<FormDef>(entity.FormId);
            if (formDef == null)
            {
                return;
            }

            var notifies = notifyRepo.Find(x =>
                x.CorpId == entity.CorpId &&
                x.AppId == entity.AppId &&
                x.FormId == entity.FormId &&
                !x.Disabled &&
                x.TriggerMode == FormNotifyTriggerMode.TimeFieldScheduled).ToList();

            foreach (var notify in notifies)
            {
                await scheduleRepo.DeleteAsync(scheduleRepo.FilterBuilder.And(
                    scheduleRepo.FilterBuilder.Eq(x => x.NotifyId, notify.Id),
                    scheduleRepo.FilterBuilder.Eq(x => x.DataId, entity.Id)), session);

                if (string.IsNullOrWhiteSpace(notify.TimeField))
                {
                    continue;
                }

                var dataMatches = FormNotifyRuntime.ShouldNotify(this.Resolver, notify, new NotifyDispatchTaskArgs
                {
                    CorpId = entity.CorpId ?? string.Empty,
                    DataId = entity.Id,
                    AppId = entity.AppId,
                    FormId = entity.FormId,
                    FormTriggerMode = FormNotifyTriggerMode.TimeFieldScheduled,
                    NewData = entity
                });
                if (!dataMatches)
                {
                    continue;
                }

                var anchorTime = FormNotifyRuntime.ExtractTimeFieldValue(entity, notify.TimeField);
                if (!anchorTime.HasValue)
                {
                    continue;
                }

                var adjustedAnchor = FormNotifyRuntime.ResolveAdjustedAnchor(notify, anchorTime.Value) ?? anchorTime.Value;
                var nextTriggerTime = FormNotifyScheduleCalculator.CalculateNextTriggerTime(notify, adjustedAnchor);
                if (!nextTriggerTime.HasValue)
                {
                    continue;
                }

                await scheduleRepo.InsertAsync(new FormNotifyScheduleItem
                {
                    NotifyId = notify.Id,
                    DataId = entity.Id,
                    AppId = notify.AppId,
                    FormId = notify.FormId,
                    CorpId = notify.CorpId,
                    TriggerMode = FormNotifyTriggerMode.TimeFieldScheduled,
                    ScheduleVersion = notify.ScheduleVersion,
                    TriggerTime = nextTriggerTime.Value,
                    AnchorTime = adjustedAnchor,
                    TimeField = notify.TimeField
                }, session);
            }
        }
    }
}
