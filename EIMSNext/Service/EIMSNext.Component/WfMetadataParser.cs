using System.Text.Json;
using System.Linq;
using EIMSNext.Common;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Plugin.Contracts;
using EIMSNext.Service.Entities;
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
            var flowData = def.Content.DeserializeFromJson<FlowData>()!;
            if (def.FlowType == FlowType.EventFlow)
            {
                ValidatePrintSources(flowData);
            }
            meta.WorkflowSetting = new WorkflowSetting
            {
                Description = flowData.WorkflowMeta?.Description,
                AllowUrge = flowData.WorkflowMeta?.AllowUrge ?? false,
                NotifyChannels = flowData.WorkflowMeta?.NotifyChannels ?? NotifyChannel.None,
                AutoProcessRule = flowData.WorkflowMeta?.AutoProcessRule ?? WorkflowAutoProcessRule.Disabled,
                WithdrawRule = flowData.WorkflowMeta?.WithdrawRule ?? WorkflowWithdrawRule.Disabled,
            };
            meta.Steps = ParseSteps(def.CorpId!, eventSetting, def.FlowType, flowData);

            return (meta, eventSetting);
        }

        private static void ValidatePrintSources(FlowData flowData)
        {
            var nodes = EnumerateNodes(flowData).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var printNode in nodes.Values.Where(x => x.NodeType == WfNodeType.Print))
            {
                var sourceId = printNode.Metadata.PrintMeta?.SourceNodeId;
                if (string.IsNullOrWhiteSpace(sourceId)
                    || !nodes.TryGetValue(sourceId, out var sourceNode))
                {
                    throw new BadRequestException($"Print node [{printNode.Id}] 的打印来源节点不存在");
                }

                if (sourceNode.NodeType is WfNodeType.Print or WfNodeType.Plugin
                    || !string.Equals(GetFormId(sourceNode), printNode.Metadata.PrintMeta?.FormId, StringComparison.OrdinalIgnoreCase)
                    || !IsPreviousNode(printNode, sourceNode, nodes))
                {
                    throw new BadRequestException($"Print node [{printNode.Id}] 的打印来源节点不合法");
                }
            }
        }

        private static IEnumerable<FlowNodeData> EnumerateNodes(FlowData flowData)
        {
            return Enumerate(flowData.StartNode)
                .Concat(flowData.Nodes.SelectMany(Enumerate))
                .Concat(Enumerate(flowData.EndNode));

            static IEnumerable<FlowNodeData> Enumerate(FlowNodeData node)
            {
                yield return node;
                if (node.ChildNodes == null) yield break;
                foreach (var child in node.ChildNodes.SelectMany(Enumerate)) yield return child;
            }
        }

        private static bool IsPreviousNode(FlowNodeData node, FlowNodeData source, IReadOnlyDictionary<string, FlowNodeData> nodes)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = node;
            while (!string.IsNullOrWhiteSpace(current.PrevId)
                && visited.Add(current.Id)
                && nodes.TryGetValue(current.PrevId, out var previous))
            {
                if (string.Equals(previous.Id, source.Id, StringComparison.OrdinalIgnoreCase)) return true;
                current = previous;
            }

            return false;
        }

        private static string? GetFormId(FlowNodeData node)
        {
            return node.NodeType switch
            {
                WfNodeType.Start => node.Metadata.TriggerMeta?.FormId,
                WfNodeType.Insert => node.Metadata.InsertMeta?.FormId,
                WfNodeType.QueryOne => node.Metadata.QueryOneMeta?.FormId,
                WfNodeType.QueryMany => node.Metadata.QueryManyMeta?.FormId,
                WfNodeType.Update => node.Metadata.UpdateMeta?.FormId,
                WfNodeType.Delete => node.Metadata.DeleteMeta?.FormId,
                _ => null,
            };
        }

        private List<WfStep> ParseSteps(string corpId, EventSetting eventSetting, FlowType flowType, FlowData flowData)
        {
            var steps = new List<WfStep>() { };
            var otherformIds = new List<string>();

            ParseFlowNode(corpId, steps, flowType, flowData.StartNode, flowData.EndNode.Id, otherformIds);
            flowData.Nodes.ForEach(node => { ParseFlowNode(corpId, steps, flowType, node, flowData.EndNode.Id, otherformIds); });
            ParseFlowNode(corpId, steps, flowType, flowData.EndNode, flowData.EndNode.Id, otherformIds);

            if (flowType == FlowType.EventFlow)
            {
                var triggerMeta = flowData.StartNode.Metadata.TriggerMeta!;

                otherformIds.Remove(triggerMeta.FormId);

                eventSetting.EventType = triggerMeta.EventType;
                eventSetting.WfNodeId = triggerMeta.WfNodeId;
                eventSetting.NodeAction = triggerMeta.NodeAction;
                eventSetting.SourceFormId = triggerMeta.FormId;
                eventSetting.OtherFormIds = otherformIds;
                eventSetting.CascadeMode = flowData.EfCascade;
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
            var prefix = flowType == FlowType.EventFlow ? "Ef" : "Wf";
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

            if (flowType == FlowType.EventFlow)
            {
                step.EfNodeSetting = GetEfNodeSetting(corpId, flowNode, otherFormIds);
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
                    var approveMeta = flowNode.Metadata.ApproveMeta;
                    var approverType = approveMeta?.ApproverType ?? ApproverType.Normal;
                    wfNodeSetting.ApproveSetting = new ApproveSetting
                    {
                        ApproverType = approverType,
                        ApprovalMode = approveMeta?.ApproveMode ?? WfApprovalMode.None,
                        Candidates = approverType == ApproverType.Normal ? approveMeta?.ApprovalCandidates ?? new List<ApprovalCandidate>() : new List<ApprovalCandidate>(),
                        ByLevelApprovalSetting = approveMeta?.ByLevelApprovalSetting,
                        EnableCopyto = approveMeta?.EnableCopyto,
                        CopytoCandidates = approveMeta?.CopytoCandidates,
                        NodeActions = approveMeta?.NodeActions?.Select(x => new NodeActionConfig
                        {
                            ActionType = Enum.TryParse<NodeActionType>(x.ActionType.ToString(), true, out var actionType) ? actionType : NodeActionType.Submit,
                            Enabled = x.Enabled ?? false,
                            Text = x.Text,
                            Candidates = x.Candidates?.ToList()
                        }).ToList(),
                        NotifyChannels = approveMeta?.NotifyChannels ?? NotifyChannel.None,
                        ExpireSetting = approveMeta?.ExpireSetting == null ? null : new ExpireSetting
                        {
                            ActionType = approveMeta.ExpireSetting.ActionType,
                            TimeValue = approveMeta.ExpireSetting.TimeValue,
                            TimeUnit = approveMeta.ExpireSetting.TimeUnit,
                            NotifySetting = approveMeta.ExpireSetting.NotifySetting == null ? null : new NotifySetting
                            {
                                Channels = approveMeta.ExpireSetting.NotifySetting.Channels,
                                Candidates = approveMeta.ExpireSetting.NotifySetting.Candidates
                            },
                            TransferSetting = approveMeta.ExpireSetting.TransferSetting == null ? null : new TransferSetting
                            {
                                Candidates = approveMeta.ExpireSetting.TransferSetting.Candidates
                            },
                            ReturnSetting = approveMeta.ExpireSetting.ReturnSetting == null ? null : new ReturnSetting
                            {
                                TargetMode = approveMeta.ExpireSetting.ReturnSetting.TargetMode,
                                TargetNodeId = approveMeta.ExpireSetting.ReturnSetting.TargetNodeId
                            }
                        },
                        SubmitCondition = ParseSubmitCondition(approveMeta?.SubmitCondition),
                        NoApproverSetting = ParseNoApproverSetting(approveMeta?.NoApproverSetting)
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
        private EfNodeSetting GetEfNodeSetting(string corpId, FlowNodeData flowNode, List<string> otherFormIds)
        {
            var efNodeSetting = new EfNodeSetting() { NodeType = flowNode.NodeType };

            switch (flowNode.NodeType)
            {
                case WfNodeType.Start:
                    efNodeSetting.SingleResult = flowNode.Metadata.TriggerMeta!.SingleResult;
                    efNodeSetting.TriggerSetting = new TriggerSetting
                    {
                        EventType = flowNode.Metadata.TriggerMeta?.EventType,
                        ChangeFields = flowNode.Metadata.TriggerMeta?.ChangeFields,
                        Condition = ParseConditionList(flowNode.Metadata.TriggerMeta?.Condition),
                        FormId = flowNode.Metadata.TriggerMeta?.FormId,
                        WfNodeId = flowNode.Metadata.TriggerMeta?.WfNodeId,
                        NodeAction = flowNode.Metadata.TriggerMeta?.NodeAction,
                        TriggerKind = flowNode.Metadata.TriggerMeta?.TriggerKind ?? EventFlowTriggerKind.Form,
                        TimeTrigger = flowNode.Metadata.TriggerMeta?.TimeSettings,
                        HttpTrigger = flowNode.Metadata.TriggerMeta?.HttpSettings,
                    };

                    break;
                case WfNodeType.Insert:
                    efNodeSetting.SingleResult = flowNode.Metadata.InsertMeta!.SingleResult;
                    efNodeSetting.InsertSetting = new InsertSetting
                    {
                        FormId = flowNode.Metadata.InsertMeta!.FormId,
                        FieldSettings = ParseFormFieldList(FlowType.EventFlow, flowNode.Metadata.InsertMeta!.FormFieldList)
                    };
                    EventFlowFieldMappingValidator.ValidateFormFieldSettings(
                        efNodeSetting.InsertSetting.FieldSettings,
                        $"Insert node [{flowNode.Id}]");
                    otherFormIds.TryAdd(efNodeSetting.InsertSetting.FormId);
                    break;
                case WfNodeType.QueryOne:
                    efNodeSetting.SingleResult = flowNode.Metadata.QueryOneMeta!.SingleResult;
                    efNodeSetting.QueryOneSetting = new QueryOneSetting
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
                    otherFormIds.TryAdd(efNodeSetting.QueryOneSetting.FormId);
                    break;
                case WfNodeType.QueryMany:
                    efNodeSetting.SingleResult = flowNode.Metadata.QueryManyMeta!.SingleResult;
                    efNodeSetting.QueryManySetting = new QueryManySetting
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
                    otherFormIds.TryAdd(efNodeSetting.QueryManySetting.FormId);
                    break;
                case WfNodeType.Delete:
                    efNodeSetting.SingleResult = flowNode.Metadata.DeleteMeta!.SingleResult;
                    efNodeSetting.DeleteSetting = new DeleteSetting
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
                    otherFormIds.TryAdd(efNodeSetting.DeleteSetting.FormId);
                    break;
                case WfNodeType.Update:
                    efNodeSetting.SingleResult = flowNode.Metadata.UpdateMeta!.SingleResult;
                    efNodeSetting.UpdateSetting = new UpdateSetting
                    {
                        UpdateMode = flowNode.Metadata.UpdateMeta!.UpdateMode,
                        NodeId = flowNode.Metadata.UpdateMeta.NodeId,
                        FormId = flowNode.Metadata.UpdateMeta!.FormId,
                        FieldSettings = ParseFormFieldList(FlowType.EventFlow, flowNode.Metadata.UpdateMeta.FormFieldList),
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

                    if (efNodeSetting.UpdateSetting.InsertIfNoData)
                        efNodeSetting.UpdateSetting.InsertFieldSettings = ParseFormFieldList(FlowType.EventFlow, flowNode.Metadata.UpdateMeta.InsertFieldList);

                    EventFlowFieldMappingValidator.ValidateFormFieldSettings(
                        efNodeSetting.UpdateSetting.FieldSettings,
                        $"Update node [{flowNode.Id}]");
                    EventFlowFieldMappingValidator.ValidateFormFieldSettings(
                        efNodeSetting.UpdateSetting.InsertFieldSettings,
                        $"Update node [{flowNode.Id}] insert-if-no-data");
                    otherFormIds.TryAdd(efNodeSetting.UpdateSetting.FormId);
                    break;
                case WfNodeType.Print:
                    var printMeta = flowNode.Metadata.PrintMeta;
                    if (printMeta == null
                        || string.IsNullOrWhiteSpace(printMeta.SourceNodeId)
                        || string.IsNullOrWhiteSpace(printMeta.FormId)
                        || string.IsNullOrWhiteSpace(printMeta.PrintDefId))
                    {
                        throw new ArgumentException($"Print node [{flowNode.Id}] is not configured");
                    }

                    efNodeSetting.SingleResult = true;
                    efNodeSetting.PrintSetting = new PrintSetting
                    {
                        SourceNodeId = printMeta.SourceNodeId,
                        FormId = printMeta.FormId,
                        PrintDefId = printMeta.PrintDefId,
                    };
                    otherFormIds.TryAdd(efNodeSetting.PrintSetting.FormId);
                    break;
                case WfNodeType.Plugin:
                    efNodeSetting.SingleResult = flowNode.Metadata.PluginMeta!.SingleResult;
                    efNodeSetting.PluginSetting = new Plugin.Contracts.PluginSetting
                    {
                        PluginId = flowNode.Metadata.PluginMeta.PluginId,
                        FunctionId = flowNode.Metadata.PluginMeta.FunctionId,
                        FieldSettings = ParsePluginFieldList(flowNode.Metadata.PluginMeta.FieldSettings),
                        ResultFields = ParsePluginResultFieldList(flowNode.Metadata.PluginMeta.ResultFields)
                    };
                    break;
            }

            return efNodeSetting;
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

            return cond.ToScriptExpression();
        }

        private SubmitConditionSetting? ParseSubmitCondition(SubmitConditionMeta? condition)
        {
            if (condition?.Enabled != true)
            {
                return null;
            }

            var expression = ParseFormulaExpression(condition.FormulaValue);
            return new SubmitConditionSetting
            {
                Enabled = true,
                Expression = string.IsNullOrWhiteSpace(expression) ? ScriptExpression.TRUE : expression,
                PromptText = condition.PromptText
            };
        }

        private NoApproverSetting ParseNoApproverSetting(NoApproverMeta? setting)
        {
            return new NoApproverSetting
            {
                ActionType = setting?.ActionType ?? NoApproverActionType.StopAndReport,
                Candidates = setting?.Candidates
            };
        }

        private string ParseFormulaExpression(FormulaValue? formulaValue)
        {
            if (formulaValue == null)
            {
                return string.Empty;
            }

            var exp = formulaValue.Expression ?? string.Empty;
            exp = SubstituteFormulaTokens(exp, formulaValue.Refs);
            return exp;
        }

        /// <summary>
        /// 把 <c>$F1</c>/<c>$F2</c>/… 占位符替换为 <c>data.{formId|nodeId}.field</c>。
        /// <para>
        /// 关键保护：
        ///  1) 长度降序处理避免 <c>$F1</c> 先替换后吞掉 <c>$F10</c> 的前缀；
        ///  2) 字面量保护：表达式体里形如 <c>"$F1"</c>/<c>'$F1'</c> 的字符串字面量先被
        ///     控制字符占位符挪走，替换完再还原，避免误改用户字符串。
        /// </para>
        /// </summary>
        private static string SubstituteFormulaTokens(string expression, List<FormulaRef> refs)
        {
            if (string.IsNullOrEmpty(expression) || refs == null || refs.Count == 0)
            {
                return expression;
            }

            // 1) 把字面量里的 $F\d+ 暂时挪走（用 ASCII 控制字符做占位符，源码中几乎不可能出现）
            var literals = new List<string>();
            var masked = System.Text.RegularExpressions.Regex.Replace(
                expression,
                @"\$F\d+",
                match =>
                {
                    var idx = literals.Count;
                    literals.Add(match.Value);
                    return $"\u0001FMLIT{idx}\u0002";
                });

            // 2) 按 token 长度降序，避免 $F1 抢先覆盖 $F10
            foreach (var formulaRef in refs.OrderByDescending(r => r.Key.Length))
            {
                if (string.IsNullOrEmpty(formulaRef.Key))
                {
                    continue;
                }
                masked = masked.Replace(formulaRef.Key, formulaRef.Field.ToFieldExp());
            }

            // 3) 还原字面量
            for (var i = 0; i < literals.Count; i++)
            {
                masked = masked.Replace($"\u0001FMLIT{i}\u0002", literals[i]);
            }

            return masked;
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
                if (valueObj.ValueField != null)
                {
                    field.ValueField = valueObj.ValueField;
                }

                result.Add(field);
            }

            return result;
        }

        private (string Exp, bool IsSubField, FormFieldValueSetting? ValueField) ParseFieldFieldValue(FormFieldItem item)
        {
            var exp = string.Empty;
            var isSubField = false;
            FormFieldValueSetting? valueField = null;

            var valueType = Enum.Parse<FieldValueType>(item.Value!.Type, true);

            switch (valueType)
            {
                case FieldValueType.Field:
                    if (item.Value.FieldValue != null)
                    {
                        exp = item.Value.FieldValue.ToFieldExp();
                        isSubField = item.Value.FieldValue.IsSubField;
                        valueField = BuildFormFieldValueSetting(item.Value.FieldValue);
                    }
                    else
                    {
                        exp = "null";
                    }
                    break;
                case FieldValueType.Formula:
                    if (item.Value.FormulaValue != null)
                    {
                        exp = item.Value.FormulaValue.Expression;
                        foreach (var formulaRef in item.Value.FormulaValue.Refs)
                        {
                            exp = exp.Replace(formulaRef.Key, formulaRef.Field.ToFieldExp());
                        }

                        if (item.Value.FormulaValue.DrivingField != null)
                        {
                            isSubField = item.Value.FormulaValue.DrivingField.IsSubField;
                            valueField = BuildFormFieldValueSetting(item.Value.FormulaValue.DrivingField);
                        }
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

            return (exp, isSubField, valueField);
        }

        private FormFieldValueSetting BuildFormFieldValueSetting(FormFieldDef field)
        {
            return new FormFieldValueSetting
            {
                Field = new FormField
                {
                    FormId = field.FormId,
                    Field = field.Field,
                    NodeId = field.NodeId,
                    Type = field.Type,
                    IsSubField = field.IsSubField
                },
                SingleResultNode = field.SingleResultNode,
            };
        }

        private List<PluginFieldSetting> ParsePluginFieldList(List<PluginFieldItem>? fieldList, bool isSubFieldSetting = false)
        {
            if (fieldList == null || fieldList.Count == 0)
            {
                return new List<PluginFieldSetting>();
            }

            return fieldList.Select(item =>
                ParsePluginFieldItem(item, isSubFieldSetting)).ToList();
        }

        private PluginFieldSetting ParsePluginFieldItem(PluginFieldItem item, bool isSubFieldSetting)
        {
            var fieldSetting = new PluginFieldSetting
            {
                FieldKey = item.FieldKey,
                FieldType = item.FieldType,
                ValueType = Enum.TryParse<PluginValueType>(item.Value?.Type, true, out var valueType)
                    ? valueType
                    : PluginValueType.Empty,
                Value = item.Value?.Value,
                SubFieldSettings = ParsePluginFieldList(item.SubFieldSettings, isSubFieldSetting: true),
            };

            if (item.Value?.FieldValue != null)
            {
                fieldSetting.ValueField = new PluginFieldReference
                {
                    NodeId = item.Value.FieldValue.NodeId ?? string.Empty,
                    FormId = item.Value.FieldValue.FormId,
                    Field = item.Value.FieldValue.Field,
                    FieldType = item.Value.FieldValue.Type,
                    IsSubField = item.Value.FieldValue.IsSubField || item.Value.FieldValue.Field.Contains('>'),
                    SingleResultNode = item.Value.FieldValue.SingleResultNode,
                };
            }

            EventFlowFieldMappingValidator.ValidatePluginFieldSetting(fieldSetting, isSubFieldSetting);
            return fieldSetting;
        }

        private List<PluginResultFieldSetting> ParsePluginResultFieldList(List<PluginResultFieldItem>? fieldList)
        {
            if (fieldList == null || fieldList.Count == 0)
            {
                return new List<PluginResultFieldSetting>();
            }

            return fieldList
                .Where(item => !string.IsNullOrWhiteSpace(item.FieldKey))
                .Select(item => new PluginResultFieldSetting
                {
                    FieldKey = item.FieldKey,
                    FieldName = string.IsNullOrWhiteSpace(item.FieldName) ? item.FieldKey : item.FieldName,
                    FieldType = item.FieldType,
                    SubFields = ParsePluginResultFieldList(item.SubFields),
                })
                .ToList();
        }
        #endregion

        #region Help Classes
        private class FlowData
        {
            public FlowNodeData StartNode { get; set; } = new FlowNodeData();
            public List<FlowNodeData> Nodes { get; set; } = new List<FlowNodeData>();
            public FlowNodeData EndNode { get; set; } = new FlowNodeData();
            public WorkflowMeta? WorkflowMeta { get; set; }
            public CascadeMode EfCascade { get; set; }
            public List<string>? EventIds { get; set; }
        }
        private class WorkflowMeta
        {
            public string? Description { get; set; }
            public bool? AllowUrge { get; set; }
            public NotifyChannel NotifyChannels { get; set; }
            public WorkflowAutoProcessRule? AutoProcessRule { get; set; }
            public WorkflowWithdrawRule? WithdrawRule { get; set; }
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
            public ApproverType ApproverType { get; set; } = ApproverType.Normal;
            public WfApprovalMode ApproveMode { get; set; }
            public List<ApprovalCandidate> ApprovalCandidates { get; set; } = new List<ApprovalCandidate>();
            public ByLevelApprovalSetting? ByLevelApprovalSetting { get; set; }
            public bool? EnableCopyto { get; set; }
            public List<ApprovalCandidate>? CopytoCandidates { get; set; }
            public List<NodeActionMeta>? NodeActions { get; set; }
            public NotifyChannel NotifyChannels { get; set; }
            public ExpireMeta? ExpireSetting { get; set; }
            public SubmitConditionMeta? SubmitCondition { get; set; }
            public NoApproverMeta? NoApproverSetting { get; set; }
        }

        private class SubmitConditionMeta
        {
            public bool? Enabled { get; set; }
            public FormulaValue? FormulaValue { get; set; }
            public string? PromptText { get; set; }
        }

        private class NoApproverMeta
        {
            public NoApproverActionType? ActionType { get; set; }
            public List<ApprovalCandidate>? Candidates { get; set; }
        }

        private class NodeActionMeta
        {
            public string ActionType { get; set; } = string.Empty;
            public bool? Enabled { get; set; }
            public string? Text { get; set; }
            public List<ApprovalCandidate>? Candidates { get; set; }
        }

        private class ExpireMeta
        {
            public WfExpireActionType ActionType { get; set; } = WfExpireActionType.AutoNotify;
            public int TimeValue { get; set; }
            public TimeUnit TimeUnit { get; set; } = TimeUnit.Minute;
            public NotifyMeta? NotifySetting { get; set; }
            public TransferMeta? TransferSetting { get; set; }
            public ReturnMeta? ReturnSetting { get; set; }
        }

        private class NotifyMeta
        {
            public NotifyChannel Channels { get; set; }
            public List<ApprovalCandidate>? Candidates { get; set; }
        }

        private class TransferMeta
        {
            public List<ApprovalCandidate>? Candidates { get; set; }
        }

        private class ReturnMeta
        {
            public ReturnTargetMode TargetMode { get; set; } = ReturnTargetMode.Previous;
            public string? TargetNodeId { get; set; }
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
            public EventFlowTriggerKind TriggerKind { get; set; } = EventFlowTriggerKind.Form;
            public EventFlowTimeTriggerSetting? TimeSettings { get; set; }
            public EventFlowHttpTriggerSetting? HttpSettings { get; set; }
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
            public FormulaValue? FormulaValue { get; set; }
        }
        private class FormulaValue
        {
            public string Expression { get; set; } = string.Empty;
            public List<FormulaRef> Refs { get; set; } = new List<FormulaRef>();
            public FormFieldDef? DrivingField { get; set; }
        }
        private class FormulaRef
        {
            public string Key { get; set; } = string.Empty;
            public FormFieldDef Field { get; set; } = new FormFieldDef();
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
            public string SourceNodeId { get; set; } = string.Empty;
            public string FormId { get; set; } = string.Empty;
            public string PrintDefId { get; set; } = string.Empty;
            public bool SingleResult { get; set; }
        }
        private class PluginMeta
        {
            public bool SingleResult { get; set; }
            public string PluginId { get; set; } = string.Empty;
            public string FunctionId { get; set; } = string.Empty;
            public List<PluginFieldItem> FieldSettings { get; set; } = new List<PluginFieldItem>();
            public List<PluginResultFieldItem> ResultFields { get; set; } = new List<PluginResultFieldItem>();
        }

        private class PluginFieldItem
        {
            public string FieldKey { get; set; } = string.Empty;
            public string FieldType { get; set; } = string.Empty;
            public FormFieldValue? Value { get; set; }
            public List<PluginFieldItem> SubFieldSettings { get; set; } = new List<PluginFieldItem>();
        }

        private class PluginResultFieldItem
        {
            public string FieldKey { get; set; } = string.Empty;
            public string? FieldName { get; set; }
            public string FieldType { get; set; } = string.Empty;
            public List<PluginResultFieldItem> SubFields { get; set; } = new List<PluginResultFieldItem>();
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

                    sortItem.Field = Fields.IsSystemField(item.Field!.Field) ? item.Field!.Field : "data." + item.Field!.Field;

                    sortList.Add(sortItem);
                }

                return sortList;
            }
        }

        #endregion
    }
}
