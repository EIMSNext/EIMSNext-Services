using EIMSNext.Common;
using EIMSNext.Common.Extension;
using EIMSNext.Core.Query;
using EIMSNext.Entity;
using EIMSNext.Scripting;
using MongoDB.Driver;

namespace EIMSNext.Component
{
    public class WfMetadataParser
    {
        #region 解析 Steps
        public (WfMetadata Metadata, EventSetting EventSetting) Parse(Wf_Definition def)
        {
            var eventSetting = new EventSetting();
            var meta = def.Metadata;
            meta.Id = def.ExternalId;
            meta.Version = def.Version;
            meta.Steps = ParseSteps(def.CorpId!, eventSetting, def.FlowType, def.Content);

            return (meta, eventSetting);
        }

        private List<WfStep> ParseSteps(string corpId, EventSetting eventSetting, FlowType flowType, string content)
        {
            var steps = new List<WfStep>() { };
            var otherformIds = new List<string>();

            var flowData = content.DeserializeFromJson<FlowData>()!;

            ParseFlowNode(corpId, steps, flowType, flowData.StartNode, flowData.EndNode.Id, otherformIds);
            flowData.Nodes.ForEach(node => { ParseFlowNode(corpId, steps, flowType, node, flowData.EndNode.Id, otherformIds); });
            ParseFlowNode(corpId, steps, flowType, flowData.EndNode, flowData.EndNode.Id, otherformIds);

            if (flowType == FlowType.Dataflow)
            {
                var triggerMeta = flowData.StartNode.Metadata.TriggerMeta!;

                otherformIds.Remove(triggerMeta.FormId);

                eventSetting.EventType = triggerMeta.EventType;
                eventSetting.WfNodeId = triggerMeta.WfNodeId;
                eventSetting.NodeAction = triggerMeta.NodeAction;
                eventSetting.SourceFormId = triggerMeta.FormId;
                eventSetting.OtherFormIds = otherformIds;
                eventSetting.CascadeMode = flowData.DfCascade;
                if (flowData.EventIds?.Count > 0)
                {
                    eventSetting.SpecifiedEvents = $",{string.Join(',', flowData.EventIds)},";
                }
            }

            return steps;
        }
        private void ParseFlowNode(string corpId, List<WfStep> steps, FlowType flowType, FlowNodeData flowNode, string endNodeId, List<string> otherFormIds)
        {
            if (flowNode.NodeType == WfNodeType.Branch || flowNode.NodeType == WfNodeType.Branch2)
            {
                ParseBranchNode(corpId, steps, flowType, flowNode, endNodeId, otherFormIds);
            }
            else
            {
                ParseNonBranchNode(corpId, steps, flowType, flowNode, endNodeId, otherFormIds);
            }
        }
        private string GetStepType(FlowType flowType, WfNodeType nodeType)
        {
            var prefix = flowType == FlowType.Dataflow ? "Df" : "Wf";
            return $"{prefix}{nodeType}Node";
        }

        private void ParseNonBranchNode(string corpId, List<WfStep> steps, FlowType flowType, FlowNodeData flowNode, string endNodeId, List<string> otherFormIds)
        {
            var step = new WfStep();
            steps.Add(step);

            step.Id = flowNode.Id;
            step.Name = flowNode.Name;
            step.NodeType = flowNode.NodeType;
            step.NextStepId = flowNode.NextId ?? "";

            //非结束结点，在没有下一节点时，将指向结束节点
            if (string.IsNullOrEmpty(step.NextStepId) && step.NodeType != WfNodeType.End)
                step.NextStepId = endNodeId;

            step.StepType = GetStepType(flowType, flowNode.NodeType);

            if (flowType == FlowType.Dataflow)
            {
                step.DfNodeSetting = GetDfNodeSetting(corpId, flowNode, otherFormIds);
            }
            else
            {
                step.WfNodeSetting = GetWfNodeSetting(corpId, flowNode);
            }
        }
        private WfNodeSetting GetWfNodeSetting(string corpId, FlowNodeData flowNode)
        {
            var wfNodeSetting = new WfNodeSetting() { NodeType = flowNode.NodeType };

            switch (flowNode.NodeType)
            {
                case WfNodeType.Approve:
                    wfNodeSetting.ApproveSetting = new ApproveSetting
                    {
                        ApprovalMode = flowNode.Metadata.ApproveMeta?.ApproveMode ?? WfApprovalMode.None,
                        Candidates = flowNode.Metadata.ApproveMeta?.ApprovalCandidates ?? new List<ApprovalCandidate>()
                    };
                    break;
                case WfNodeType.CopyTo:
                    wfNodeSetting.CopyToSetting = new CopyToSetting
                    {
                        Candidates = flowNode.Metadata.CopyToMeta?.ApprovalCandidates ?? new List<ApprovalCandidate>()
                    };
                    break;
            }

            return wfNodeSetting;
        }
        private DfNodeSetting GetDfNodeSetting(string corpId, FlowNodeData flowNode, List<string> otherFormIds)
        {
            var dfNodeSetting = new DfNodeSetting() { NodeType = flowNode.NodeType };

            switch (flowNode.NodeType)
            {
                case WfNodeType.Start:
                    dfNodeSetting.SingleResult = flowNode.Metadata.TriggerMeta!.SingleResult;
                    dfNodeSetting.TriggerSetting = new TriggerSetting
                    {
                        EventType = flowNode.Metadata.TriggerMeta?.EventType,
                        ChangeFields = flowNode.Metadata.TriggerMeta?.ChangeFields,
                        Condition = ParseConditionList(flowNode.Metadata.TriggerMeta?.Condition),
                        FormId = flowNode.Metadata.TriggerMeta?.FormId,
                        WfNodeId = flowNode.Metadata.TriggerMeta?.WfNodeId,
                        NodeAction = flowNode.Metadata.TriggerMeta?.NodeAction,
                    };

                    break;
                case WfNodeType.Insert:
                    dfNodeSetting.SingleResult = flowNode.Metadata.InsertMeta!.SingleResult;
                    dfNodeSetting.InsertSetting = new InsertSetting
                    {
                        FormId = flowNode.Metadata.InsertMeta!.FormId,
                        FieldSettings = ParseFormFieldList(FlowType.Dataflow, flowNode.Metadata.InsertMeta!.FormFieldList)
                    };
                    otherFormIds.TryAdd(dfNodeSetting.InsertSetting.FormId);
                    break;
                case WfNodeType.QueryOne:
                    dfNodeSetting.SingleResult = flowNode.Metadata.QueryOneMeta!.SingleResult;
                    dfNodeSetting.QueryOneSetting = new QueryOneSetting
                    {
                        FormId = flowNode.Metadata.QueryOneMeta!.FormId,
                        DynamicFindOptions = new DynamicFindOptions<FormData>
                        {
                            Filter = new DynamicFilter
                            {
                                Rel = FilterRel.And,
                                Items = new List<DynamicFilter> {
                                    new DynamicFilter{ Field="corpId", Op= FilterOp.Eq, Value=corpId },
                                    new DynamicFilter { Field="formId", Op= FilterOp.Eq, Value=flowNode.Metadata.QueryOneMeta.FormId},
                                    flowNode.Metadata.QueryOneMeta.Condition.ToDynamicFilter() }
                            },
                            Sort = flowNode.Metadata.QueryOneMeta.Sort == null ? null : flowNode.Metadata.QueryOneMeta.Sort.ToDynamicSortList(),
                            Take = 1
                        }.SerializeToJson()
                    };
                    otherFormIds.TryAdd(dfNodeSetting.QueryOneSetting.FormId);
                    break;
                case WfNodeType.QueryMany:
                    dfNodeSetting.SingleResult = flowNode.Metadata.QueryManyMeta!.SingleResult;
                    dfNodeSetting.QueryManySetting = new QueryManySetting
                    {
                        FormId = flowNode.Metadata.QueryManyMeta!.FormId,
                        DynamicFindOptions = new DynamicFindOptions<FormData>
                        {
                            Filter = new DynamicFilter
                            {
                                Rel = FilterRel.And,
                                Items = new List<DynamicFilter> {
                                    new DynamicFilter{ Field="corpId", Op= FilterOp.Eq, Value=corpId },
                                    new DynamicFilter { Field="formId", Op= FilterOp.Eq, Value=flowNode.Metadata.QueryManyMeta.FormId},
                                    flowNode.Metadata.QueryManyMeta.Condition.ToDynamicFilter() }
                            },
                            Sort = flowNode.Metadata.QueryManyMeta.Sort == null ? null : flowNode.Metadata.QueryManyMeta.Sort.ToDynamicSortList(),
                            Take = flowNode.Metadata.QueryManyMeta.Take,
                        }.SerializeToJson()
                    };
                    otherFormIds.TryAdd(dfNodeSetting.QueryManySetting.FormId);
                    break;
                case WfNodeType.Delete:
                    dfNodeSetting.SingleResult = flowNode.Metadata.DeleteMeta!.SingleResult;
                    dfNodeSetting.DeleteSetting = new DeleteSetting
                    {
                        DeleteMode = flowNode.Metadata.DeleteMeta!.DeleteMode,
                        NodeId = flowNode.Metadata.DeleteMeta.NodeId,
                        FormId = flowNode.Metadata.DeleteMeta!.FormId,
                        DynamicFindOptions = flowNode.Metadata.DeleteMeta.DeleteMode == UpdateMode.Form ? new DynamicFindOptions<FormData>
                        {
                            Filter = new DynamicFilter
                            {
                                Rel = FilterRel.And,
                                Items = new List<DynamicFilter> {
                                    new DynamicFilter{ Field="corpId", Op= FilterOp.Eq, Value=corpId },
                                    new DynamicFilter { Field="formId", Op= FilterOp.Eq, Value=flowNode.Metadata.DeleteMeta.FormId},
                                    flowNode.Metadata.DeleteMeta.Condition!.ToDynamicFilter() }
                            }
                        }.SerializeToJson() : null
                    };
                    otherFormIds.TryAdd(dfNodeSetting.DeleteSetting.FormId);
                    break;
                case WfNodeType.Update:
                    dfNodeSetting.SingleResult = flowNode.Metadata.UpdateMeta!.SingleResult;
                    dfNodeSetting.UpdateSetting = new UpdateSetting
                    {
                        UpdateMode = flowNode.Metadata.UpdateMeta!.UpdateMode,
                        NodeId = flowNode.Metadata.UpdateMeta.NodeId,
                        FormId = flowNode.Metadata.UpdateMeta!.FormId,
                        FieldSettings = ParseFormFieldList(FlowType.Dataflow, flowNode.Metadata.UpdateMeta.FormFieldList),
                        UpdateMatch = flowNode.Metadata.UpdateMeta!.SubCondition?.ToDataMatchSetting() ?? new DataMatchSetting(),
                        DynamicFindOptions = flowNode.Metadata.UpdateMeta.UpdateMode == UpdateMode.Form ? new DynamicFindOptions<FormData>
                        {
                            Filter = new DynamicFilter
                            {
                                Rel = FilterRel.And,
                                Items = new List<DynamicFilter> {
                                    new DynamicFilter{ Field="corpId", Op= FilterOp.Eq, Value=corpId },
                                    new DynamicFilter { Field="formId", Op= FilterOp.Eq, Value=flowNode.Metadata.UpdateMeta.FormId},
                                    flowNode.Metadata.UpdateMeta.Condition!.ToDynamicFilter() }
                            }
                        }.SerializeToJson() : null,
                        InsertIfNoData = flowNode.Metadata.UpdateMeta.InsertIfNoData,
                    };

                    if (dfNodeSetting.UpdateSetting.InsertIfNoData)
                        dfNodeSetting.UpdateSetting.InsertFieldSettings = ParseFormFieldList(FlowType.Dataflow, flowNode.Metadata.UpdateMeta.InsertFieldList);

                    otherFormIds.TryAdd(dfNodeSetting.UpdateSetting.FormId);
                    break;
            }

            return dfNodeSetting;
        }
        private void ParseBranchNode(string corpId, List<WfStep> steps, FlowType flowType, FlowNodeData flowNode, string endNodeId, List<string> otherFormIds)
        {
            if (flowNode.ChildNodes?.Count > 0)
            {
                var condNodes = flowNode.ChildNodes.Where(x => x.ConditionData?.NodeType == WfNodeType.Condition);
                var otherCondNode = flowNode.ChildNodes.FirstOrDefault(x => x.ConditionData?.NodeType == WfNodeType.ConditionOther);

                //如果Else分支不存在或没有子节点，则视为没有节点
                if (otherCondNode?.ChildNodes?.Count == 0) otherCondNode = null;

                var nextStepId = flowNode.NextId ?? endNodeId;
                var defaultNextStepId = otherCondNode?.ChildNodes?.FirstOrDefault()?.Id ?? nextStepId;

                var step = new WfStep();
                steps.Add(step);

                step.Id = flowNode.Id;
                step.Name = flowNode.Name;
                step.NodeType = flowNode.NodeType;

                //原始Decide分支无法跳转到Else分支，使用重写的Node
                step.StepType = "WfDecideNode";

                //当所有分支不匹配时，跳转到Else分支
                step.NextStepId = defaultNextStepId;

                var selectNext = new Dictionary<string, string>();
                foreach (var branch in condNodes)
                {
                    if (branch.ChildNodes?.Count > 0)
                    {
                        selectNext.Add(branch.ChildNodes.First().Id, ParseConditionToExpression(flowType, branch.ConditionData));
                        branch.ChildNodes.ForEach(b => ParseFlowNode(corpId, steps, flowType, b, nextStepId, otherFormIds));
                    }
                }
                if (otherCondNode != null && otherCondNode.ChildNodes?.Count > 0)
                {
                    selectNext.Add(defaultNextStepId, $" (data.matched_result==false) ");
                    otherCondNode.ChildNodes.ForEach(b => ParseFlowNode(corpId, steps, flowType, b, nextStepId, otherFormIds));
                    step.NextStepId = "";
                }
                step.SelectNextStep = selectNext;
            }
        }

        #endregion

        #region 解析分支条件表达式
        private string ParseConditionToExpression(FlowType flowType, FlowNodeData? condNode)
        {
            if (condNode == null)
                return ScriptExpression.FALSE;

            if (condNode.NodeType == WfNodeType.ConditionOther)
                return ScriptExpression.TRUE;

            if (condNode.Metadata.ConditionMeta?.Condition == null)
                return ScriptExpression.FALSE;

            return ParseConditionList(condNode.Metadata.ConditionMeta.Condition);
        }
        private string ParseConditionList(ConditionList? cond)
        {
            if (cond == null) return ScriptExpression.TRUE;

            if (cond.Items != null && cond.Items.Count > 0)
            {
                List<string> subExps = new List<string>();
                cond.Items.ForEach(x => subExps.Add(ParseConditionList(x)));

                var rel = (cond.Rel == FilterRel.Or) ? "||" : "&&";
                return string.Join(rel, subExps.Select(x => $"({x})"));
            }
            else
            {
                if (string.IsNullOrEmpty(cond.Field?.Field))
                    return ScriptExpression.TRUE;

                return FormatExp(cond.Field, cond.Op ?? FilterOp.Eq, cond.Value);
            }
        }
        private string FormatExp(FormFieldDef condField, string op, ConditionValue? condValue)
        {
            var field = condField.ToFieldExp();
            var oper = ParseOp(condField.Type, op);
            var value = ParseValue(condField.Type, condValue, out FieldValueType valueType);

            var exp = "";

            if ((condField.IsSubField))
            {
                //子表字段使用Match方法计算
                var fields = field.Split('>', StringSplitOptions.RemoveEmptyEntries);
                var arrField = fields[0];
                var subField = fields[1];
                var subExp = "";

                if (valueType == FieldValueType.Field)
                {
                    var valueFields = value.Split('>', StringSplitOptions.RemoveEmptyEntries);
                    var valueArrField = valueFields[0];
                    var valueSubField = valueFields[1];

                    switch (op)
                    {
                        //TODO: 值为字段时，需要更详细的处理
                        case FilterOp.In:
                        case FilterOp.Nin:
                            subExp = $"MATCH({valueArrField}, y=>{{return {oper}(y.{valueSubField},x.{subField})}})";
                            break;
                        default:
                            subExp = $"MATCH({valueArrField}, y=>{{return {oper}(x.{subField},y.{valueSubField})}})";
                            break;
                    }
                }
                else
                {
                    switch (op)
                    {
                        case FilterOp.Empty:
                        case FilterOp.NotEmpty:
                            subExp = $" {oper}(x.{subField}) ";
                            break;
                        case FilterOp.In:
                        case FilterOp.Nin:
                            subExp = $" {oper}({value},x.{subField}) ";
                            break;
                        default:
                            subExp = $" {oper}(x.{subField},{value}) ";
                            break;
                    }
                }

                exp = $"MATCH({arrField}, x=>{{return {subExp}}})";
            }
            else
            {
                if (valueType == FieldValueType.Field)
                {
                    var valueFields = value.Split('>', StringSplitOptions.RemoveEmptyEntries);
                    var valueArrField = valueFields[0];
                    var valueSubField = valueFields[1];

                    switch (op)
                    {
                        //TODO: 值为字段时，需要更详细的处理
                        case FilterOp.In:
                        case FilterOp.Nin:
                            exp = $"MATCH({valueArrField}, y=>{{return {oper}(y.{valueSubField},{field})}})";
                            break;
                        default:
                            exp = $"MATCH({valueArrField}, y=>{{return {oper}({field},y.{valueSubField})}})";
                            break;
                    }
                }
                else
                {
                    switch (op)
                    {
                        case FilterOp.Empty:
                        case FilterOp.NotEmpty:
                            exp = $" {oper}({field}) ";
                            break;
                        case FilterOp.In:
                        case FilterOp.Nin:
                            exp = $" {oper}({value},{field}) ";
                            break;
                        default:
                            exp = $" {oper}({field},{value}) ";
                            break;
                    }
                }
            }

            return exp;
        }

        private string ParseOp(string fieldType, string op)
        {
            var oper = op.ToUpper();
            switch (op.ToLower())
            {
                case FilterOp.Lte:
                    oper = "LE";
                    break;
                case FilterOp.Gte:
                    oper = "GE";
                    break;
            }

            return oper;
        }
        private string ParseValue(string fieldType, ConditionValue? value, out FieldValueType valueType)
        {
            valueType = FieldValueType.Custom;

            if (value == null) return string.Empty;

            valueType = string.IsNullOrEmpty(value.Type) ? FieldValueType.Custom : Enum.Parse<FieldValueType>(value.Type, true);

            if (valueType == FieldValueType.Field)
            {
                return value.FieldValue!.ToFieldExp();
            }
            else
            {
                var fType = fieldType.ToLower();
                var valStr = "";
                if (fType == FieldType.Number)
                    valStr = $"{value?.Value ?? "0"}";
                else
                    valStr = $"'{value?.Value}'";

                return valStr;
            }
        }
        #endregion

        #region 解析表字段
        private List<FormFieldSetting> ParseFormFieldList(FlowType flowType, FormFieldList fieldList)
        {
            var result = new List<FormFieldSetting>();

            foreach (var item in fieldList.Items)
            {
                var valueObj = ParseFieldFieldValue(item);
                var field = new FormFieldSetting()
                {
                    Field = new FormField
                    {
                        FormId = item.Field!.FormId,
                        Field = item.Field.Field,
                        Type = item.Field.Type,
                        IsSubField = item.Field.IsSubField
                    },
                    ValueType = Enum.Parse<FieldValueType>(item.Value!.Type, true),
                    ValueExp = valueObj.Exp
                };
                if (field.ValueType == FieldValueType.Field)
                {
                    field.ValueField = new FormFieldValueSetting
                    {
                        Field = new FormField
                        {
                            FormId = item.Value.FieldValue!.FormId,
                            Field = item.Value.FieldValue!.Field,
                            NodeId = item.Value.FieldValue!.NodeId,
                            Type = item.Value.FieldValue!.Type,
                            IsSubField = item.Value.FieldValue!.IsSubField
                        },
                        SingleResultNode = item.Value.FieldValue!.SingleResultNode,
                    };
                }

                result.Add(field);
            }

            return result;
        }

        private (string Exp, bool IsSubField) ParseFieldFieldValue(FormFieldItem item)
        {
            var exp = string.Empty;
            var isSubField = false;

            var valueType = Enum.Parse<FieldValueType>(item.Value!.Type, true);

            switch (valueType)
            {
                case FieldValueType.Field:
                    if (item.Value.FieldValue != null)
                    {
                        exp = item.Value.FieldValue.ToFieldExp();
                        isSubField = item.Value.FieldValue.IsSubField;
                    }
                    else
                    {
                        exp = "null";
                    }
                    break;
                case FieldValueType.Empty:
                    exp = "null";
                    break;
                default:// FieldValueType.Custom:
                    {
                        var fType = item.Field!.Type;
                        if (fType == FieldType.Number)
                            exp = $"{item.Value.Value ?? "0"}";
                        else
                            exp = $"'{item.Value.Value}'";
                    }
                    break;
            }

            return (exp, isSubField);
        }
        #endregion

        #region Help Classes
        private class FlowData
        {
            public FlowNodeData StartNode { get; set; } = new FlowNodeData();
            public List<FlowNodeData> Nodes { get; set; } = new List<FlowNodeData>();
            public FlowNodeData EndNode { get; set; } = new FlowNodeData();
            public CascadeMode DfCascade { get; set; }
            public List<string>? EventIds { get; set; }
        }
        private class FlowNodeData
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Notes { get; set; }
            public string? PrevId { get; set; }
            public string? NextId { get; set; }
            public WfNodeType NodeType { get; set; }
            public FlowNodeData? ConditionData { get; set; }
            public List<FlowNodeData>? ChildNodes { get; set; }
            public FlowNodeMetaData Metadata { get; set; } = new FlowNodeMetaData();
        }

        private class FlowNodeMetaData
        {
            //Cond
            public ConditionMeta? ConditionMeta { get; set; }

            //WF
            public ApproveMeta? ApproveMeta { get; set; }
            public CopytoMeta? CopyToMeta { get; set; }

            //DF
            public TriggerMeta? TriggerMeta { get; set; }
            public InsertMeta? InsertMeta { get; set; }
            public UpdateMeta? UpdateMeta { get; set; }
            public DeleteMeta? DeleteMeta { get; set; }
            public QueryOneMeta? QueryOneMeta { get; set; }
            public QueryManyMeta? QueryManyMeta { get; set; }
            public PrintMeta? PrintMeta { get; set; }
            public PluginMeta? PluginMeta { get; set; }
        }
        private class ConditionMeta
        {
            public ConditionList? Condition { get; set; }
        }
        private class ApproveMeta
        {
            public WfApprovalMode ApproveMode { get; set; }
            public List<ApprovalCandidate> ApprovalCandidates { get; set; } = new List<ApprovalCandidate>();
            public bool? EnableCopyto { get; set; }
            public List<ApprovalCandidate>? CopytoCandidates { get; set; }
        }
        private class CopytoMeta
        {
            public List<ApprovalCandidate> ApprovalCandidates { get; set; } = new List<ApprovalCandidate>();
        }
        private class TriggerMeta
        {
            public EventType EventType { get; set; }
            public string FormId { get; set; } = string.Empty;
            /// <summary>
            /// 节点流转时节点ID
            /// </summary>
            public string WfNodeId { get; set; } = string.Empty;
            /// <summary>
            /// 节点流转时节点操作，提交或退回
            /// </summary>
            public string NodeAction { get; set; } = string.Empty;
            /// <summary>
            /// 触发条件
            /// </summary>
            public ConditionList? Condition { get; set; }
            /// <summary>
            /// 数据修改时，哪些字段修改会触发
            /// </summary>
            public List<string>? ChangeFields { get; set; }
            public bool SingleResult { get; set; }
        }

        private class InsertMeta
        {
            public string FormId { get; set; } = string.Empty;
            public FormFieldList FormFieldList { get; set; } = new FormFieldList();
            public bool SingleResult { get; set; }
        }
        private class FormFieldList
        {
            public List<FormFieldItem> Items { get; set; } = new List<FormFieldItem> { };
        }
        private class FormFieldItem
        {
            public FormFieldDef? Field { get; set; }
            public FormFieldValue? Value { get; set; }
        }
        private class FormFieldValue
        {
            public string Type { get; set; } = string.Empty;
            public object? Value { get; set; }
            public FormFieldDef? FieldValue { get; set; }
        }
        private class FormFieldDef
        {
            public string FormId { get; set; } = string.Empty;
            public string Field { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public bool IsSubField { get; set; }
            public string? NodeId { get; set; } = string.Empty;
            public bool? SingleResultNode { get; set; }

            public string ToFieldExp()
            {
                if (!string.IsNullOrEmpty(NodeId))
                    return $"data.n_{NodeId}.{Field}";

                return $"data.f_{FormId}.{Field}";
            }
        }
        private class UpdateMeta
        {
            public UpdateMode UpdateMode { get; set; }
            public string? NodeId { get; set; }
            public string FormId { get; set; } = string.Empty;
            public ConditionList? Condition { get; set; }
            public FormFieldList FormFieldList { get; set; } = new FormFieldList();
            public ConditionList? SubCondition { get; set; }
            public bool SingleResult { get; set; }
            public bool InsertIfNoData { get; set; }
            public FormFieldList InsertFieldList { get; set; } = new FormFieldList();
        }
        private class DeleteMeta
        {
            public UpdateMode DeleteMode { get; set; }
            public string? NodeId { get; set; }
            public string FormId { get; set; } = string.Empty;
            public ConditionList? Condition { get; set; }
            public bool SingleResult { get; set; }
        }
        private class QueryOneMeta
        {
            public string FormId { get; set; } = string.Empty;
            public ConditionList Condition { get; set; } = new ConditionList();
            public FieldSortList? Sort { get; set; }
            public bool SingleResult { get; set; } = true;
        }
        private class QueryManyMeta
        {
            public string FormId { get; set; } = string.Empty;
            public ConditionList Condition { get; set; } = new ConditionList();
            public FieldSortList? Sort { get; set; }
            public int Take { get; set; }
            public bool SingleResult { get; set; } = false;
        }
        private class PrintMeta
        {
            public bool SingleResult { get; set; }
        }
        private class PluginMeta
        {
            public bool SingleResult { get; set; }
        }

        private class ConditionList
        {
            public string? Rel { get; set; }
            public List<ConditionList>? Items { get; set; }

            public FormFieldDef? Field { get; set; }
            public string? Op { get; set; }
            public ConditionValue? Value { get; set; }

            public DynamicFilter ToDynamicFilter()
            {
                var filter = new DynamicFilter();

                if (Items?.Count > 0)
                {
                    filter.Rel = string.IsNullOrEmpty(Rel) ? FilterRel.And : Rel;
                    filter.Items = new List<DynamicFilter>();
                    Items.ForEach(x => filter.Items.Add(x.ToDynamicFilter()));
                }
                else if (Field != null)
                {
                    filter.Field = Constants.SystemFields.Contains(Field.Field, StringComparer.OrdinalIgnoreCase) ? Field.Field : "data." + Field.Field;
                    filter.Type = Field.Type;
                    filter.Op = Op;
                    filter.Value = ParseValue(Value, out FieldValueType valueType);
                    filter.ValueIsExp = valueType != FieldValueType.Custom;
                    filter.ValueIsField = valueType == FieldValueType.Field;
                }

                return filter;
            }
            private object? ParseValue(ConditionValue? value, out FieldValueType valueType)
            {
                valueType = FieldValueType.Custom;
                object? exp = null;

                if (value != null)
                {
                    valueType = Enum.Parse<FieldValueType>(value.Type!, true);

                    switch (valueType)
                    {
                        case FieldValueType.Field:
                            exp = value.FieldValue!.ToFieldExp();
                            break;
                        case FieldValueType.Empty:
                            exp = "null";
                            break;
                        default:// FieldValueType.Custom:
                            exp = value.Value;
                            break;
                    }
                }

                return exp;
            }

            public DataMatchSetting ToDataMatchSetting()
            {
                var match = new DataMatchSetting() { Rel = Rel, Op = Op };

                if (Items?.Count > 0)
                {
                    match.Rel = string.IsNullOrEmpty(Rel) ? FilterRel.And : Rel;
                    match.Items = new List<DataMatchSetting>();
                    Items.ForEach(x => match.Items.Add(x.ToDataMatchSetting()));
                }
                else if (Field != null)
                {
                    match.Field = new FormField
                    {
                        FormId = Field.FormId,
                        NodeId = Field.NodeId,
                        Field = Field.Field,
                        Type = Field.Type,
                        IsSubField = Field.IsSubField
                    };
                    match.Op = Op;
                    if (Value != null)
                    {
                        var value = ParseValue(Value, out FieldValueType valueType);
                        match.Value = new DataMatchValueSetting { Type = valueType, Value = value };

                        if (valueType == FieldValueType.Field)
                        {
                            match.Value.FieldValue = new FormField
                            {
                                FormId = Value.FieldValue!.FormId,
                                NodeId = Value.FieldValue.NodeId,
                                Field = Value.FieldValue.Field,
                                Type = Value.FieldValue.Type,
                                IsSubField = Value.FieldValue.IsSubField
                            };
                        }
                    }
                }

                return match;
            }
        }

        private class ConditionValue
        {
            public string? Type { get; set; }
            public object? Value { get; set; }
            public FormFieldDef? FieldValue { get; set; }
        }

        private class FieldSortItem
        {
            public FormFieldDef? Field { get; set; }
            public SortDir Sort { get; set; }
        }

        private class FieldSortList
        {
            public IList<FieldSortItem> Items { get; set; } = new List<FieldSortItem>();

            public DynamicSortList ToDynamicSortList()
            {
                var sortList = new DynamicSortList();

                foreach (var item in Items)
                {
                    var sortItem = new DynamicSort() { Dir = item.Sort };

                    sortItem.Field = Constants.SystemFields.Contains(item.Field!.Field, StringComparer.OrdinalIgnoreCase) ? item.Field!.Field : "data." + item.Field!.Field;

                    sortList.Add(sortItem);
                }

                return sortList;
            }
        }

        #endregion
    }
}
