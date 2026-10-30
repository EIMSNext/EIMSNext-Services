using System.Dynamic;
using System.Text.Json;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Flow.Core.Nodes.EventFlow;
using EIMSNext.Scripting;
using HKH.Common;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using EIMSNext.Core.Extensions;
using EIMSNext.Async.Abstractions.System;
using EIMSNext.ApiClient.File;

namespace EIMSNext.Flow.Core.Nodes
{
    public abstract class EfNodeBase<T> : NodeBase where T : NodeBase
    {
        protected EfNodeBase(IResolver resolver) : base(resolver)
        {
            RunLogNodeRepository = resolver.GetRepository<Ef_RunLogNode>();
            FormDataRepository = resolver.GetRepository<FormData>();
            FormDefRepository = resolver.GetRepository<FormDef>();
            ScriptEngine = resolver.Resolve<IScriptEngine>();
            Logger = resolver.GetLogger<T>();
        }

        protected IRepository<Ef_RunLogNode> RunLogNodeRepository { get; private set; }
        protected IRepository<FormData> FormDataRepository { get; private set; }
        protected IRepository<FormDef> FormDefRepository { get; private set; }
        protected IScriptEngine ScriptEngine { get; private set; }

        protected ILogger<T> Logger { get; private set; }

        protected EfDataContext GetDataContext(IStepExecutionContext context)
        {
            return (EfDataContext)context.Workflow.Data;
        }

        /// <summary>
        /// 带执行日志的节点执行包装（异步）。
        /// </summary>
        protected async Task<ExecutionResult> ExecuteWithLogAsync(IStepExecutionContext context, Func<EfDataContext, Task<ExecutionResult>> action, string successSummary = "执行成功")
        {
            var dataContext = GetDataContext(context);
            var startTime = DateTime.UtcNow.ToTimeStampMs();

            try
            {
                var result = await action(dataContext);
                var endTime = DateTime.UtcNow.ToTimeStampMs();
                await CreateExecLogAsync(context.Workflow, dataContext, Metadata!, string.Empty, startTime, endTime, summary: successSummary);
                return result;
            }
            catch (Exception ex)
            {
                var endTime = DateTime.UtcNow.ToTimeStampMs();
                var failure = ClassifyFailure(Metadata!, ex);
                dataContext.ErrMsg = failure.Reason;
                await CreateExecLogAsync(
                    context.Workflow,
                    dataContext,
                    Metadata!,
                    ex.Message,
                    startTime,
                    endTime,
                    failure.Reason,
                    failure.Suggestion,
                    failure.Summary);
                throw;
            }
        }

        /// <summary>
        /// 节点失败日志（异步）：直接同步写库会阻塞线程，且 PostgreSQL 侧只有异步仓储接口。
        /// </summary>
        protected async Task CreateFailureExecLogAsync(
            WorkflowInstance wfInst,
            EfDataContext dataContext,
            WfStep wfStep,
            string errMsg,
            long startTime,
            long endTime,
            bool pluginFailure = false)
        {
            var failure = ClassifyFailure(wfStep, null, errMsg, pluginFailure);
            dataContext.ErrMsg = failure.Reason;
            await CreateExecLogAsync(wfInst, dataContext, wfStep, errMsg, startTime, endTime, failure.Reason, failure.Suggestion, failure.Summary);
        }

        protected Dictionary<string, object> GetNodeScriptData(EfDataContext dataContext)
        {
            var wrapData = new Dictionary<string, object?>();
            foreach (var item in dataContext.NodeDatas)
            {
                if (item.Value.ActionDatas.Count > 0)
                {
                    if (item.Value.SingleResult) //只有单个的会直接参与公式运算？
                    {
                        var formData = item.Value.ActionDatas.First().FormData;
                        var pData = WithCreateBy(formData);

                        wrapData.TryAdd($"n_{item.Value.NodeId}", pData);
                    }
                    else
                    {
                        var list = new List<Dictionary<string, object?>>();
                        item.Value.ActionDatas.ForEach(actionData =>
                        {
                            list.Add(WithCreateBy(actionData.FormData));
                        });

                        wrapData.TryAdd($"n_{item.Value.NodeId}", list);
                    }
                }
            }

            return new Dictionary<string, object>() { ["data"] = wrapData };
        }

        /// <summary>
        /// 取一份带 <c>createBy</c> 的数据副本供脚本使用。
        /// </summary>
        /// <remarks>
        /// 原先直接对 <see cref="FormData.Data"/> 做 <c>TryAdd("createBy", …)</c>，
        /// 把审计对象写进了业务数据字典：它会随 Data 序列化进 jsonb，还会改动被 EF 跟踪的实体。
        /// 在副本上补充，脚本表达式照旧可以引用 createBy。
        /// </remarks>
        private static Dictionary<string, object?> WithCreateBy(FormData formData)
        {
            var data = new Dictionary<string, object?>(formData.Data);
            data.TryAdd("createBy", formData.CreateBy);
            return data;
        }

        /// <summary>
        /// 打印产物惰性上传：值若是待上传标记（<see cref="EfPendingPrintFile.MarkerKey"/>），
        /// 首次消费时经 FileApiClient 上传并把附件信息缓存到 run 上下文；同一 run 多个消费点共享一次上传。
        /// run 跨进程恢复（重试/暂停）后内存产物丢失时，按 marker 持久化要素就地重渲染（重复打印无副作用）。
        /// </summary>
        protected object? MaterializePrintOutput(EfDataContext dataContext, object? value)
        {
            var marker = ExtractPendingPrintMarker(value);
            if (marker == null
                || !marker.TryGetValue(EfPendingPrintFile.MarkerKey, out var nodeId)
                || nodeId is not string printNodeId)
            {
                return value;
            }

            if (!dataContext.PendingPrints.TryGetValue(printNodeId, out var pending))
            {
                pending = new EfPrintRenderer(Resolver).RenderFromMarker(dataContext, marker);
                dataContext.PendingPrints[printNodeId] = pending;
            }

            if (pending.Attachment == null)
            {
                var accessToken = Resolver.Resolve<ISystemTokenProvider>()
                    .GetAccessTokenAsync(dataContext.CorpId, "ef", dataContext.EventFlowId)
                    .GetAwaiter().GetResult();
                var attachment = Resolver.Resolve<IFileClient>()
                    .Upload(pending.Content, pending.FileName, accessToken)
                    .GetAwaiter().GetResult()
                    ?? throw new InvalidOperationException("附件上传失败：File.Host 未返回附件信息");

                pending.Attachment = new Dictionary<string, object?>
                {
                    ["id"] = attachment.Id,
                    ["name"] = attachment.FileName,
                    ["fileName"] = attachment.FileName,
                    ["savePath"] = attachment.SavePath,
                    ["thumbPath"] = attachment.ThumbPath,
                    ["fileExt"] = attachment.FileExt,
                    ["fileSize"] = attachment.FileSize,
                    ["url"] = attachment.SavePath,
                    ["thumbUrl"] = attachment.ThumbPath,
                };
            }

            return new Dictionary<string, object?>(pending.Attachment);
        }

        /// <summary>
        /// 从待上传标记中提取完整 marker 字典。脚本求值结果经 V8 往返后可能是
        /// IDictionary、JsonElement 或 ClearScript 宿主对象，分别识别；非标记返回 null。
        /// </summary>
        private static Dictionary<string, object?>? ExtractPendingPrintMarker(object? value)
        {
            switch (value)
            {
                case null:
                case string:
                case char:
                case bool:
                case byte:
                case short:
                case int:
                case long:
                case float:
                case double:
                case decimal:
                    return null;
                case IDictionary<string, object?> dict:
                    return dict.TryGetValue(EfPendingPrintFile.MarkerKey, out var entry) && entry is string
                        ? new Dictionary<string, object?>(dict)
                        : null;
                case JsonElement { ValueKind: JsonValueKind.Object } json:
                    return json.TryGetProperty(EfPendingPrintFile.MarkerKey, out var prop) && prop.ValueKind == JsonValueKind.String
                        ? json.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.ToString())
                        : null;
                default:
                    try
                    {
                        var json = value.SerializeToJson();
                        if (json == null || !json.Contains(EfPendingPrintFile.MarkerKey))
                            return null;

                        return json.DeserializeFromJson<Dictionary<string, object?>>();
                    }
                    catch
                    {
                        return null;
                    }
            }
        }

        /// <summary>
        /// 写入节点执行日志（异步）。
        /// </summary>
        protected async Task CreateExecLogAsync(
            WorkflowInstance wfInst,
            EfDataContext dataContext,
            WfStep wfStep,
            string errMsg = "",
            long? startTime = null,
            long? endTime = null,
            string failureReason = "",
            string troubleshootingSuggestion = "",
            string summary = "")
        {
            Ef_RunLogNode? runLogNode = null;
            try
            {
                var finishedAt = endTime ?? DateTime.UtcNow.ToTimeStampMs();
                var startedAt = startTime ?? finishedAt;
                var success = string.IsNullOrEmpty(errMsg);
                runLogNode = new Ef_RunLogNode()
                {
                    Id = string.Empty,
                    RunLogId = dataContext.RunLogId,
                    CorpId = dataContext.CorpId,
                    EventFlowId = dataContext.EventFlowId,
                    DataId = dataContext.DataId ?? "",
                    WfInstanceId = wfInst.Id,
                    NodeId = wfStep.Id,
                    NodeName = wfStep.Name,
                    NodeType = wfStep.NodeType,
                    StartTime = startedAt,
                    EndTime = finishedAt,
                    ExecTime = finishedAt,
                    ErrMsg = errMsg,
                    FailureReason = failureReason,
                    TroubleshootingSuggestion = troubleshootingSuggestion,
                    Summary = string.IsNullOrWhiteSpace(summary) ? (success ? "执行成功" : failureReason) : summary,
                    Success = success
                };
                var existed = RunLogNodeRepository.Find(x =>
                        x.RunLogId == runLogNode.RunLogId
                        && x.NodeId == runLogNode.NodeId)
                    .FirstOrDefault();
                if (existed == null)
                {
                    await RunLogNodeRepository.InsertAsync(runLogNode);
                }
                else
                {
                    runLogNode.Id = existed.Id;
                    runLogNode.CreateBy = existed.CreateBy;
                    runLogNode.CreateTime = existed.CreateTime;
                    runLogNode.UpdateBy = existed.UpdateBy;
                    runLogNode.UpdateTime = existed.UpdateTime;
                    runLogNode.DeleteFlag = existed.DeleteFlag;
                    runLogNode.DeleteBy = existed.DeleteBy;
                    runLogNode.DeleteTime = existed.DeleteTime;
                    runLogNode.CorpId = existed.CorpId;
                    await RunLogNodeRepository.ReplaceAsync(runLogNode);
                }
            }
            catch (Exception ex)    //写日志失败不影响整个数据流程
            {
                Logger.LogError(ex, "写入数据流程执行日志失败。RunLogNode={RunLogNode}", runLogNode);
            }
        }

        protected NodeFailureInfo ClassifyFailure(WfStep wfStep, Exception? ex, string? errMsg = null, bool pluginFailure = false)
        {
            var summary = NormalizeError(errMsg ?? ex?.Message);
            var message = summary.ToLowerInvariant();

            if (IsFormMissing(message))
            {
                return new NodeFailureInfo("节点引用的表单不存在", "检查目标表单或触发表单是否已删除或无权限访问", summary);
            }

            if (IsFieldMissing(ex, message))
            {
                return new NodeFailureInfo("节点中引用的字段不存在", "检查节点中使用的字段是否已经被删除", summary);
            }

            if (pluginFailure || wfStep.NodeType == WfNodeType.Plugin)
            {
                return new NodeFailureInfo(summary, "检查插件、插件函数、订阅/配置和入参映射", summary);
            }

            if (IsConditionOrDataMutationNode(wfStep.NodeType))
            {
                return new NodeFailureInfo(summary, "检查筛选条件中的字段、比较符和值来源", summary);
            }

            return new NodeFailureInfo(summary, "检查该节点配置及前置节点输出数据", summary);
        }

        private static bool IsFormMissing(string message)
        {
            return message.Contains("表单定义不存在")
                || message.Contains("表单不存在")
                || message.Contains("form definition")
                || message.Contains("form not found");
        }

        private static bool IsFieldMissing(Exception? ex, string message)
        {
            return (ex is KeyNotFoundException && !message.Contains("n_"))
                || message.Contains("字段不存在")
                || message.Contains("字段被删除")
                || message.Contains("已删除字段")
                || message.Contains("field not found")
                || message.Contains("does not exist")
                || message.Contains("not present in the dictionary");
        }

        private static bool IsConditionOrDataMutationNode(WfNodeType nodeType)
        {
            return nodeType == WfNodeType.QueryOne
                || nodeType == WfNodeType.QueryMany
                || nodeType == WfNodeType.Update
                || nodeType == WfNodeType.Delete
                || nodeType == WfNodeType.Insert;
        }

        private static string NormalizeError(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "执行失败";
            }

            var firstLine = message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? message;
            return firstLine.Length > 300 ? $"{firstLine[..300]}..." : firstLine;
        }

        protected record NodeFailureInfo(string Reason, string Suggestion, string Summary);

        #region Form
        protected FormData? GetFormData(string dataId)
        {
            //此处只需要基本数据字段+Data, 其他可不要
            //将来可换成Find
            return FormDataRepository.Get(dataId);
        }
        private FormDef? GetFormDef(string formId)
        {
            //此处只需要基本数据字段+Content.Items, 其他可不要
            //将来可换成Find
            return FormDefRepository.Get(formId);
        }
        protected FormDef GetFormDef(EfDataContext dataContext, string formId)
        {
            if (!dataContext.FormDefs.ContainsKey(formId))
            {
                var formDef = GetFormDef(formId);
                if (formDef == null)
                {
                    throw new UnLogException("表单定义不存在");
                }

                dataContext.FormDefs.Add(formId, formDef);
            }

            return dataContext.FormDefs[formId];
        }
        #endregion

        #region Filter
        protected void BuildDynamicFilter(EfDataContext dataContext, DynamicFilter filter, Dictionary<string, object> data)
        {
            filter.Value = DynamicValueNormalizer.Normalize(filter.Value);
            if (filter.ValueIsExp)
            {
                filter.Value = EvalFilterValue(dataContext, filter.ValueIsField, filter.Value!.ToString()!, data);
            }

            if (filter.IsGroup && filter.Items?.Count > 0)
            {
                filter.Items.ForEach(x => BuildDynamicFilter(dataContext, x, data));
            }
        }
        protected object? EvalFilterValue(EfDataContext dataContext, bool isFieldExp, string script, Dictionary<string, object> data)
        {
            if (isFieldExp && script.Contains('>')) //子表字段
            {
                var valueFields = script.Split('>', StringSplitOptions.RemoveEmptyEntries);
                var valueArrField = valueFields[0];
                var valueSubField = valueFields[1];

                var arr = ScriptEngine.Evaluate($"MAP({valueArrField},'{valueSubField}')", data);
                if (string.IsNullOrEmpty(arr.Value))
                    return new List<object>();

                return arr.Value!.ToString().DeserializeFromJson<List<object>>();
            }

            return MaterializePrintOutput(dataContext, ScriptEngine.Evaluate(script, data).Value);
        }
        #endregion

        #region Insert Data
        protected List<ActionFormData> BuildInsertDatas(EfDataContext dataContext, FormDef formDef, List<FormFieldSetting> fieldSettings)
        {
            var insertDatas = new List<ActionFormData>();

            var scriptData = GetNodeScriptData(dataContext);

            var multiData = fieldSettings.Any(x => !x.Field.IsSubField && (!x.ValueIsSingleResultNode() || x.ValueIsSubField()));
            //映射 M->M, M->S, S->S, (S->M, MM->M)
            if (multiData)
            {
                var multiDataNodeId = fieldSettings.FirstOrDefault(x => !x.Field.IsSubField && !x.ValueIsSingleResultNode())?.ValueField?.Field.NodeId;
                if (!string.IsNullOrEmpty(multiDataNodeId))
                {
                    //MM -> M
                    var nodeDatas = dataContext.NodeDatas[multiDataNodeId].ActionDatas;
                    for (var mi = 0; mi < nodeDatas.Count; mi++)
                    {
                        var insertData = new FormData()
                        {
                            AppId = dataContext.AppId,
                            CorpId = dataContext.CorpId,
                            FormId = formDef.Id,
                            Data = new Dictionary<string, object?>(),
                            CreateBy = dataContext.WfStarter,
                            CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                        };

                        //逐条填充字段
                        InsertFormData(insertData, dataContext, scriptData, formDef, fieldSettings, mi);

                        insertDatas.Add(new ActionFormData { FormData = insertData, State = DataState.Inserted });
                    }
                }
                else
                {
                    //S -> M
                    //先创建多个主表记录
                    var subfieldSettings = fieldSettings.Where(x => x.ValueIsSubField());
                    var subfieldSetting = subfieldSettings.First();
                    var subNodeData = dataContext.NodeDatas.ContainsKey(subfieldSetting.ValueField!.Field.NodeId!) ? dataContext.NodeDatas[subfieldSetting.ValueField!.Field.NodeId!].ActionDatas.FirstOrDefault()?.FormData : null;
                    var itemCount = 1; //主记录条数
                    if (subNodeData != null)
                    {
                        subfieldSettings.ForEach(x =>
                        {
                            var subArrField = x.ValueField!.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries);
                            var subMainField = subArrField[0];
                            if (subNodeData.Data.ContainsKey(subMainField))
                            {
                                var subValArrData = subNodeData.Data.GetValueOrDefault<IEnumerable<object>>(subMainField);
                                itemCount = Math.Max(itemCount, subValArrData?.Count() ?? 0);
                            }
                        });
                    }

                    for (int mi = 0; mi < itemCount; mi++)
                    {
                        var insertData = new FormData()
                        {
                            AppId = dataContext.AppId,
                            CorpId = dataContext.CorpId,
                            FormId = formDef.Id,
                            Data = new Dictionary<string, object?>(),
                            CreateBy = dataContext.WfStarter,
                            CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                        };

                        //逐条填充字段
                        InsertFormData(insertData, dataContext, scriptData, formDef, fieldSettings, mi, subNodeData);

                        insertDatas.Add(new ActionFormData { FormData = insertData, State = DataState.Inserted });
                    }
                }
            }
            else
            {
                var insertData = new FormData()
                {
                    AppId = dataContext.AppId,
                    CorpId = dataContext.CorpId,
                    FormId = formDef.Id,
                    Data = new Dictionary<string, object?>(),
                    CreateBy = dataContext.WfStarter,
                    CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                };

                InsertFormData(insertData, dataContext, scriptData, formDef, fieldSettings);

                insertDatas.Add(new ActionFormData { FormData = insertData, State = DataState.Inserted });
            }

            //计算表单内公式字段
            //Resolver.Resolve<FormulaEvaluator>().Evaluate(formDef, [insertData]);

            return insertDatas;
        }

        protected void InsertFormData(FormData insertData, EfDataContext dataContext, Dictionary<string, object>? scriptData, FormDef formDef, List<FormFieldSetting> fieldSettings, int mIndex = -1, FormData? subNodeData = null)
        {
            if (subNodeData != null)
            {
                //主模式：S -> M
                foreach (var fieldSetting in fieldSettings)
                {
                    if (fieldSetting.Field.IsSubField)
                    {
                        var field = fieldSetting.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries);
                        var mainField = field[0];//.Replace("data.", "");
                        var arrData = insertData.Data.GetRows(mainField);
                        if (!insertData.Data.ContainsKey(mainField))
                            insertData.Data.AddOrUpdate(mainField, arrData);

                        if (fieldSetting.ValueIsSubField())
                        {
                            // S->S
                            SetSubToSub(dataContext, insertData, arrData, field[1], fieldSetting, subNodeData, scriptData);
                        }
                        else
                        {
                            //M->S
                            SetMainToSub(dataContext, insertData, arrData, field[1], fieldSetting, scriptData);
                        }
                    }
                    else
                    {
                        if (fieldSetting.ValueIsSubField())
                        {
                            //S->M
                            SetSubToMain(dataContext, insertData, mIndex, fieldSetting, subNodeData, scriptData);
                        }
                        else
                        {
                            //M->M
                            SetMainToMain(dataContext, insertData, fieldSetting, scriptData);
                        }
                    }
                }
            }
            else if (mIndex > -1)
            {
                //主模式：MM -> M
                foreach (var fieldSetting in fieldSettings)
                {
                    if (fieldSetting.Field.IsSubField)
                    {
                        var field = fieldSetting.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries);
                        var mainField = field[0];//.Replace("data.", "");
                        var arrData = insertData.Data.GetRows(mainField);
                        if (!insertData.Data.ContainsKey(mainField))
                            insertData.Data.AddOrUpdate(mainField, arrData);

                        if (fieldSetting.ValueIsSubField())
                        {
                            // S->S
                            if (dataContext.NodeDatas.ContainsKey(fieldSetting.ValueField!.Field.NodeId!))
                            {
                                var valSubData = dataContext.NodeDatas[fieldSetting.ValueField!.Field.NodeId!].ActionDatas.FirstOrDefault()?.FormData;
                                SetSubToSub(dataContext, insertData, arrData, field[1], fieldSetting, valSubData, scriptData);
                            }
                        }
                        else
                        {
                            //M->S
                            SetMainToSub(dataContext, insertData, arrData, field[1], fieldSetting, scriptData);
                        }
                    }
                    else
                    {
                        if (fieldSetting.ValueIsSubField())
                        {
                            //S->M ? 此分支是不是不应该有？
                            if (dataContext.NodeDatas.ContainsKey(fieldSetting.ValueField!.Field.NodeId!))
                            {
                                var valSubData = dataContext.NodeDatas[fieldSetting.ValueField!.Field.NodeId!].ActionDatas.FirstOrDefault()?.FormData;
                                SetSubToMain(dataContext, insertData, mIndex, fieldSetting, valSubData, scriptData);
                            }
                        }
                        else
                        {
                            if (!fieldSetting.ValueIsSingleResultNode())
                            {
                                //MM->M
                                SetMultiMainToMain(dataContext, insertData, mIndex, fieldSetting, scriptData);
                            }
                            else
                            {
                                //M->M
                                SetMainToMain(dataContext, insertData, fieldSetting, scriptData);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var fieldSetting in fieldSettings)
                {
                    if (fieldSetting.Field.IsSubField)
                    {
                        var field = fieldSetting.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries);
                        var mainField = field[0];//.Replace("data.", "");
                        var arrData = insertData.Data.GetRows(mainField);
                        if (!insertData.Data.ContainsKey(mainField))
                            insertData.Data.AddOrUpdate(mainField, arrData);

                        if (fieldSetting.ValueIsSubField())
                        {
                            // S->S
                            if (dataContext.NodeDatas.ContainsKey(fieldSetting.ValueField!.Field.NodeId!))
                            {
                                var valSubData = dataContext.NodeDatas[fieldSetting.ValueField!.Field.NodeId!].ActionDatas.FirstOrDefault()?.FormData;
                                SetSubToSub(dataContext, insertData, arrData, field[1], fieldSetting, valSubData, scriptData);
                            }
                        }
                        else
                        {
                            //M->S
                            SetMainToSub(dataContext, insertData, arrData, field[1], fieldSetting, scriptData);
                        }
                    }
                    else
                    {
                        //M->M
                        SetMainToMain(dataContext, insertData, fieldSetting, scriptData);
                    }
                }
            }
        }

        /// <summary>
        /// 主表单字段对主表单字段
        /// </summary>
        protected void SetMainToMain(EfDataContext dataContext, FormData target, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            switch (fieldSetting.ValueType)
            {
                case FieldValueType.Empty:
                    {
                        target.Data.AddOrUpdate(fieldSetting.Field.Field, null);
                    }
                    break;
                case FieldValueType.Field:
                case FieldValueType.Formula: //公式与字段引用一样依赖上下文数据，公式里引用了 n_xxx 节点数据
                    {
                        target.Data.AddOrUpdate(fieldSetting.Field.Field, MaterializePrintOutput(dataContext, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp, scriptData).Value));
                    }
                    break;
                default: // FieldValueType.Custom
                    {
                        target.Data.AddOrUpdate(fieldSetting.Field.Field, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp).Value);
                    }
                    break;
            }
        }
        /// <summary>
        /// 主表单字段对子表单字段
        /// </summary>
        protected void SetMainToSub(EfDataContext dataContext, FormData target, List<Dictionary<string, object?>> subForm, string subField, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            var value = MaterializePrintOutput(dataContext, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp, scriptData).Value);
            if (subForm.Count > 0)
            {
                subForm.ForEach(x => x.AddOrUpdate(subField, value));
            }
            else
            {
                var subData = new Dictionary<string, object?>();
                subData.AddOrUpdate(subField, value);
                subForm.Add(subData);
            }
        }
        /// <summary>
        /// 子表单字段对子表单字段
        /// </summary>
        protected void SetSubToSub(EfDataContext dataContext, FormData target, List<Dictionary<string, object?>> subForm, string subField, FormFieldSetting fieldSetting, FormData? source, Dictionary<string, object>? scriptData)
        {
            var valArrField = fieldSetting.ValueField!.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries);
            var valMainField = valArrField[0];//.Replace("data.", "");

            var valSubData = source;
            if (valSubData != null && valSubData.Data.ContainsKey(valMainField))
            {
                var valArrData = valSubData.Data.GetValueOrDefault<IEnumerable<object>>(valMainField);
                if (valArrData != null)
                {
                    for (var i = 0; i < valArrData.Count(); i++)
                    {
                        var subData = new Dictionary<string, object?>();
                        if (subForm.Count() > i)
                            subData = subForm.ElementAt(i);
                        else
                        {
                            if (subForm.Count() > 0)
                            {     //复制数据，因为如果其他字段为主表字段，则需要每一行都被赋值
                                subData = subForm.ElementAt(i - 1).SerializeToJson().DeserializeFromJson<Dictionary<string, object?>>()
                                    ?? new Dictionary<string, object?>();
                            }

                            subForm.Add(subData);
                        }

                        subData.AddOrUpdate(subField, MaterializePrintOutput(dataContext, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp.Replace(">", $"[{i}]."), scriptData).Value));
                    }
                }
            }
        }
        /// <summary>
        /// 子表单字段对主表单字段
        /// </summary>
        protected void SetSubToMain(EfDataContext dataContext, FormData target, int mIndex, FormFieldSetting fieldSetting, FormData? source, Dictionary<string, object>? scriptData)
        {
            var valArrField = fieldSetting.ValueField!.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries);
            var valMainField = valArrField[0];//.Replace("data.", "");

            var valSubData = source;
            if (valSubData != null && valSubData.Data.ContainsKey(valMainField))
            {
                var valArrData = valSubData.Data.GetValueOrDefault<IEnumerable<object>>(valMainField);
                if (valArrData != null && valArrData.Count() > mIndex)
                {
                    target.Data.AddOrUpdate(fieldSetting.Field.Field, MaterializePrintOutput(dataContext, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp.Replace(">", $"[{mIndex}]."), scriptData).Value));
                }
            }
        }
        /// <summary>
        /// 多条记录的主表单(SingleResultNode=false)对主表单字段
        /// </summary>
        protected void SetMultiMainToMain(EfDataContext dataContext, FormData target, int mIndex, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            var valueField = fieldSetting.ValueField!;
            var valueExp = $"data.n_{valueField.Field.NodeId}[{mIndex}].{valueField.Field.Field}";
            target.Data.AddOrUpdate(fieldSetting.Field.Field, MaterializePrintOutput(dataContext, (object?)ScriptEngine.Evaluate(valueExp, scriptData).Value));
        }

        /// <summary>
        /// 主表单字段对子表单字段
        /// </summary>
        protected void UpdateMainToSub(EfDataContext dataContext, IDictionary<string, object?> subItem, string subField, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            subItem.AddOrUpdate(subField, MaterializePrintOutput(dataContext, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp, scriptData).Value));
        }

        /// <summary>
        /// 多条记录的主表单(SingleResultNode=false)对主表单字段
        /// </summary>
        protected void UpdateMultiMainToSub(EfDataContext dataContext, IDictionary<string, object?> subItem, int mIndex, string subField, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            var valueField = fieldSetting.ValueField!;
            var valueExp = $"data.n_{valueField.Field.NodeId}[{mIndex}].{valueField.Field.Field}";
            subItem.AddOrUpdate(subField, MaterializePrintOutput(dataContext, (object?)ScriptEngine.Evaluate(valueExp, scriptData).Value));
        }

        /// <summary>
        /// 子表单字段对子表单字段
        /// </summary>
        protected void UpdateSubToSub(EfDataContext dataContext, IDictionary<string, object?> subItem, string subField, FormFieldSetting fieldSetting, int i, Dictionary<string, object>? scriptData)
        {
            subItem.AddOrUpdate(subField, MaterializePrintOutput(dataContext, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp.Replace(">", $"[{i}]."), scriptData).Value));
        }

        #endregion
    }
}
