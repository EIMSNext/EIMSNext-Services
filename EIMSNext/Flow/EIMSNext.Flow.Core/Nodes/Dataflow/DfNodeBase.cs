using System.Dynamic;
using System.Text.Json;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Entities;
using EIMSNext.Flow.Core.Nodes.Dataflow;
using EIMSNext.Scripting;
using HKH.Common;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    public abstract class DfNodeBase<T> : NodeBase where T : NodeBase
    {
        protected DfNodeBase(IResolver resolver) : base(resolver)
        {
            RunLogNodeRepository = resolver.GetRepository<Df_RunLogNode>();
            FormDataRepository = resolver.GetRepository<FormData>();
            FormDefRepository = resolver.GetRepository<FormDef>();
            ScriptEngine = resolver.Resolve<IScriptEngine>();
            Logger = resolver.GetLogger<T>();
        }

        protected IRepository<Df_RunLogNode> RunLogNodeRepository { get; private set; }
        protected IRepository<FormData> FormDataRepository { get; private set; }
        protected IRepository<FormDef> FormDefRepository { get; private set; }
        protected IScriptEngine ScriptEngine { get; private set; }

        protected ILogger<T> Logger { get; private set; }

        protected DfDataContext GetDataContext(IStepExecutionContext context)
        {
            return (DfDataContext)context.Workflow.Data;
        }

        protected ExecutionResult ExecuteWithLog(IStepExecutionContext context, Func<DfDataContext, ExecutionResult> action, string successSummary = "执行成功")
        {
            var dataContext = GetDataContext(context);
            var startTime = DateTime.UtcNow.ToTimeStampMs();

            try
            {
                var result = action(dataContext);
                var endTime = DateTime.UtcNow.ToTimeStampMs();
                CreateExecLog(context.Workflow, dataContext, Metadata!, string.Empty, startTime, endTime, summary: successSummary);
                return result;
            }
            catch (Exception ex)
            {
                var endTime = DateTime.UtcNow.ToTimeStampMs();
                var failure = ClassifyFailure(Metadata!, ex);
                dataContext.ErrMsg = failure.Reason;
                CreateExecLog(
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

        protected void CreateFailureExecLog(
            WorkflowInstance wfInst,
            DfDataContext dataContext,
            WfStep wfStep,
            string errMsg,
            long startTime,
            long endTime,
            bool pluginFailure = false)
        {
            var failure = ClassifyFailure(wfStep, null, errMsg, pluginFailure);
            dataContext.ErrMsg = failure.Reason;
            CreateExecLog(wfInst, dataContext, wfStep, errMsg, startTime, endTime, failure.Reason, failure.Suggestion, failure.Summary);
        }

        protected Dictionary<string, object> GetNodeScriptData(DfDataContext dataContext)
        {
            var wrapData = new ExpandoObject();
            foreach (var item in dataContext.NodeDatas)
            {
                if (item.Value.ActionDatas.Count > 0)
                {
                    if (item.Value.SingleResult) //只有单个的会直接参与公式运算？
                    {
                        var formData = item.Value.ActionDatas.First().FormData;
                        var pData = formData.Data;
                        pData.TryAdd("createBy", formData.CreateBy);

                        wrapData.TryAdd($"n_{item.Value.NodeId}", pData);
                    }
                    else
                    {
                        var list = new List<ExpandoObject>();
                        item.Value.ActionDatas.ForEach(actionData =>
                        {
                            var pData = actionData.FormData.Data;
                            pData.TryAdd("createBy", actionData.FormData.CreateBy);

                            list.Add(pData);
                        });

                        wrapData.TryAdd($"n_{item.Value.NodeId}", list);
                    }
                }
            }

            return new Dictionary<string, object>() { ["data"] = wrapData };
        }

        protected void CreateExecLog(
            WorkflowInstance wfInst,
            DfDataContext dataContext,
            WfStep wfStep,
            string errMsg = "",
            long? startTime = null,
            long? endTime = null,
            string failureReason = "",
            string troubleshootingSuggestion = "",
            string summary = "")
        {
            Df_RunLogNode? runLogNode = null;
            try
            {
                var finishedAt = endTime ?? DateTime.UtcNow.ToTimeStampMs();
                var startedAt = startTime ?? finishedAt;
                var success = string.IsNullOrEmpty(errMsg);
                runLogNode = new Df_RunLogNode()
                {
                    Id = string.Empty,
                    RunLogId = dataContext.RunLogId,
                    CorpId = dataContext.CorpId,
                    DataflowId = dataContext.DataflowId,
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
                RunLogNodeRepository.Insert(runLogNode);
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
        protected FormDef GetFormDef(DfDataContext dataContext, string formId)
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
        protected void BuildDynamicFilter(DynamicFilter filter, Dictionary<string, object> data)
        {
            filter.Value = DynamicValueNormalizer.Normalize(filter.Value);
            if (filter.ValueIsExp)
            {
                filter.Value = EvalFilterValue(filter.ValueIsField, filter.Value!.ToString()!, data);
            }

            if (filter.IsGroup && filter.Items?.Count > 0)
            {
                filter.Items.ForEach(x => BuildDynamicFilter(x, data));
            }
        }
        protected object? EvalFilterValue(bool isFieldExp, string script, Dictionary<string, object> data)
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

            return ScriptEngine.Evaluate(script, data).Value;
        }
        #endregion

        #region Insert Data
        protected List<ActionFormData> BuildInsertDatas(DfDataContext dataContext, FormDef formDef, List<FormFieldSetting> fieldSettings)
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
                            Data = new ExpandoObject(),
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
                            Data = new ExpandoObject(),
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
                    Data = new ExpandoObject(),
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

        protected void InsertFormData(FormData insertData, DfDataContext dataContext, Dictionary<string, object>? scriptData, FormDef formDef, List<FormFieldSetting> fieldSettings, int mIndex = -1, FormData? subNodeData = null)
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
                        List<ExpandoObject> arrData = new List<ExpandoObject>();
                        if (insertData.Data.ContainsKey(mainField))
                            arrData = insertData.Data.GetValue<List<ExpandoObject>>(mainField, arrData);
                        else
                            insertData.Data.AddOrUpdate(mainField, arrData);

                        if (fieldSetting.ValueIsSubField())
                        {
                            // S->S
                            SetSubToSub(insertData, arrData, field[1], fieldSetting, subNodeData, scriptData);
                        }
                        else
                        {
                            //M->S
                            SetMainToSub(insertData, arrData, field[1], fieldSetting, scriptData);
                        }
                    }
                    else
                    {
                        if (fieldSetting.ValueIsSubField())
                        {
                            //S->M
                            SetSubToMain(insertData, mIndex, fieldSetting, subNodeData, scriptData);
                        }
                        else
                        {
                            //M->M
                            SetMainToMain(insertData, fieldSetting, scriptData);
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
                        List<ExpandoObject> arrData = new List<ExpandoObject>();
                        if (insertData.Data.ContainsKey(mainField))
                            arrData = insertData.Data.GetValue<List<ExpandoObject>>(mainField, arrData);
                        else
                            insertData.Data.AddOrUpdate(mainField, arrData);

                        if (fieldSetting.ValueIsSubField())
                        {
                            // S->S
                            if (dataContext.NodeDatas.ContainsKey(fieldSetting.ValueField!.Field.NodeId!))
                            {
                                var valSubData = dataContext.NodeDatas[fieldSetting.ValueField!.Field.NodeId!].ActionDatas.FirstOrDefault()?.FormData;
                                SetSubToSub(insertData, arrData, field[1], fieldSetting, valSubData, scriptData);
                            }
                        }
                        else
                        {
                            //M->S
                            SetMainToSub(insertData, arrData, field[1], fieldSetting, scriptData);
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
                                SetSubToMain(insertData, mIndex, fieldSetting, valSubData, scriptData);
                            }
                        }
                        else
                        {
                            if (!fieldSetting.ValueIsSingleResultNode())
                            {
                                //MM->M
                                SetMultiMainToMain(insertData, mIndex, fieldSetting, scriptData);
                            }
                            else
                            {
                                //M->M
                                SetMainToMain(insertData, fieldSetting, scriptData);
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
                        List<ExpandoObject> arrData = new List<ExpandoObject>();
                        if (insertData.Data.ContainsKey(mainField))
                            arrData = insertData.Data.GetValue<List<ExpandoObject>>(mainField, arrData);
                        else
                            insertData.Data.AddOrUpdate(mainField, arrData);

                        if (fieldSetting.ValueIsSubField())
                        {
                            // S->S
                            if (dataContext.NodeDatas.ContainsKey(fieldSetting.ValueField!.Field.NodeId!))
                            {
                                var valSubData = dataContext.NodeDatas[fieldSetting.ValueField!.Field.NodeId!].ActionDatas.FirstOrDefault()?.FormData;
                                SetSubToSub(insertData, arrData, field[1], fieldSetting, valSubData, scriptData);
                            }
                        }
                        else
                        {
                            //M->S
                            SetMainToSub(insertData, arrData, field[1], fieldSetting, scriptData);
                        }
                    }
                    else
                    {
                        //M->M
                        SetMainToMain(insertData, fieldSetting, scriptData);
                    }
                }
            }
        }

        /// <summary>
        /// 主表单字段对主表单字段
        /// </summary>
        protected void SetMainToMain(FormData target, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            switch (fieldSetting.ValueType)
            {
                case FieldValueType.Empty:
                    {
                        target.Data.AddOrUpdate(fieldSetting.Field.Field, null);
                    }
                    break;
                case FieldValueType.Field:
                    {
                        target.Data.AddOrUpdate(fieldSetting.Field.Field, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp, scriptData).Value);
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
        protected void SetMainToSub(FormData target, List<ExpandoObject> subForm, string subField, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            var value = (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp, scriptData).Value;
            if (subForm.Count > 0)
            {
                subForm.ForEach(x => x.AddOrUpdate(subField, value));
            }
            else
            {
                var subData = new ExpandoObject();
                subData.AddOrUpdate(subField, value);
                subForm.Add(subData);
            }
        }
        /// <summary>
        /// 子表单字段对子表单字段
        /// </summary>
        protected void SetSubToSub(FormData target, List<ExpandoObject> subForm, string subField, FormFieldSetting fieldSetting, FormData? source, Dictionary<string, object>? scriptData)
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
                        var subData = new ExpandoObject();
                        if (subForm.Count() > i)
                            subData = subForm.ElementAt(i);
                        else
                        {
                            if (subForm.Count() > 0)
                            {     //复制数据，因为如果其他字段为主表字段，则需要每一行都被赋值
                                subData = subForm.ElementAt(i - 1).SerializeToJson().DeserializeFromJson<ExpandoObject>()!;
                            }

                            subForm.Add(subData);
                        }

                        subData.AddOrUpdate(subField, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp.Replace(">", $"[{i}]."), scriptData).Value);
                    }
                }
            }
        }
        /// <summary>
        /// 子表单字段对主表单字段
        /// </summary>
        protected void SetSubToMain(FormData target, int mIndex, FormFieldSetting fieldSetting, FormData? source, Dictionary<string, object>? scriptData)
        {
            var valArrField = fieldSetting.ValueField!.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries);
            var valMainField = valArrField[0];//.Replace("data.", "");

            var valSubData = source;
            if (valSubData != null && valSubData.Data.ContainsKey(valMainField))
            {
                var valArrData = valSubData.Data.GetValueOrDefault<IEnumerable<object>>(valMainField);
                if (valArrData != null && valArrData.Count() > mIndex)
                {
                    target.Data.AddOrUpdate(fieldSetting.Field.Field, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp.Replace(">", $"[{mIndex}]."), scriptData).Value);
                }
            }
        }
        /// <summary>
        /// 多条记录的主表单(SingleResultNode=false)对主表单字段
        /// </summary>
        protected void SetMultiMainToMain(FormData target, int mIndex, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            var valueField = fieldSetting.ValueField!;
            var valueExp = $"data.n_{valueField.Field.NodeId}[{mIndex}].{valueField.Field.Field}";
            target.Data.AddOrUpdate(fieldSetting.Field.Field, (object?)ScriptEngine.Evaluate(valueExp, scriptData).Value);
        }

        /// <summary>
        /// 主表单字段对子表单字段
        /// </summary>
        protected void UpdateMainToSub(ExpandoObject subItem, string subField, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            subItem.AddOrUpdate(subField, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp, scriptData).Value);
        }

        /// <summary>
        /// 多条记录的主表单(SingleResultNode=false)对主表单字段
        /// </summary>
        protected void UpdateMultiMainToSub(ExpandoObject subItem, int mIndex, string subField, FormFieldSetting fieldSetting, Dictionary<string, object>? scriptData)
        {
            var valueField = fieldSetting.ValueField!;
            var valueExp = $"data.n_{valueField.Field.NodeId}[{mIndex}].{valueField.Field.Field}";
            subItem.AddOrUpdate(subField, (object?)ScriptEngine.Evaluate(valueExp, scriptData).Value);
        }

        /// <summary>
        /// 子表单字段对子表单字段
        /// </summary>
        protected void UpdateSubToSub(ExpandoObject subItem, string subField, FormFieldSetting fieldSetting, int i, Dictionary<string, object>? scriptData)
        {
            subItem.AddOrUpdate(subField, (object?)ScriptEngine.Evaluate(fieldSetting.ValueExp.Replace(">", $"[{i}]."), scriptData).Value);
        }

        #endregion
    }
}
