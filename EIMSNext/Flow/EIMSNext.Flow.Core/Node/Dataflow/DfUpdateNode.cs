using System.Dynamic;

using EIMSNext.Common;
using EIMSNext.Common.Extension;
using EIMSNext.Core.Query;
using EIMSNext.Entity;
using EIMSNext.Flow.Core.Node.Dataflow;
using EIMSNext.Scripting;
using HKH.Mef2.Integration;
using MongoDB.Driver;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Node
{
    public class DfUpdateNode : DfNodeBase<DfUpdateNode>
    {
        public DfUpdateNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            var dataContext = GetDataContext(context);
            var updateSetting = Metadata!.DfNodeSetting!.UpdateSetting!;
            var formDef = GetFormDef(dataContext, updateSetting.FormId);
            var actionDatas = new List<ActionFormData>();

            List<ActionFormData>? toUpdates = null;
            if (updateSetting.UpdateMode == UpdateMode.Node)
            {
                //TODO: 是否需要复制副本?
                toUpdates = dataContext.NodeDatas.FirstOrDefault(x => x.Key == updateSetting.NodeId).Value.ActionDatas.ToList();
            }
            else if (updateSetting.UpdateMode == UpdateMode.Form)
            {
                var findOpt = Metadata!.DfNodeSetting!.UpdateSetting!.DynamicFindOptions!.DeserializeFromJson<DynamicFindOptions<FormData>>()!;
                BuildDynamicFilter(findOpt.Filter!, GetNodeScriptData(dataContext));

                toUpdates = new List<ActionFormData>();
                FormDataRepository.Find(findOpt).ToList().ForEach(x => toUpdates.Add(new ActionFormData { State = DataState.Unchanged, FormData = x }));
            }

            //1 执行查询
            //2 判断执行结果
            //  2.1 如果没有可更新数据
            //      2.1.1 插入为false, 返回
            //      2.1.2 插入为true, 插入数据
            //  2.2 有数据
            //3 检查字段映射
            //  3.1 没有子条件，则全为主字段非关联映射，直接循环topupdate 更新即可
            //  3.2 有子条件，
            //      3.2.1 如果子条件字段为主字段，则循环关联数据源去匹配toupdate中记录
            //          3.2.1.1 匹配则更新
            //          3.2.1.2 不匹配，如果插入为true，则插入记录
            //      3.2.2 如果子条件字段为子表单子段，则循环toupdate中每记录的子表单
            //          3.2.2.1 匹配则更新子表单对应记录
            //          3.2.2.2 不匹配，如果插入为true, 则增加一条子表单记录

            if (toUpdates?.Count > 0)
            {
                if (updateSetting.FieldSettings.Count > 0)
                {
                    var scriptData = GetNodeScriptData(dataContext);

                    if (updateSetting.UpdateMatch.IsEmpty())
                    {
                        //M -> M, 没有匹配条件，则源表为单条数据节点
                        toUpdates.ForEach(toUpdate =>
                        {
                            toUpdate.State = DataState.Modified;
                            actionDatas.Add(toUpdate);

                            updateSetting.FieldSettings.ForEach(x =>
                            {
                                //M->M
                                SetMainToMain(toUpdate.FormData, x, scriptData);
                            });
                        });
                    }
                    else if (updateSetting.UpdateMatch.IsSubFieldMatch())
                    {
                        //子表单字段更新
                        toUpdates.ForEach(x =>
                        {
                            if (x.State == DataState.Unchanged)
                                x.State = DataState.Modified;

                            actionDatas.Add(x);
                        });

                        var multiDataFieldSetting = updateSetting.FieldSettings.FirstOrDefault(x => !x.ValueIsSingleResultNode());
                        if (multiDataFieldSetting == null)
                        {
                            //S -> S，此时源表为单条数据节点
                            var subField = updateSetting.UpdateMatch.FirstSubField();
                            var valSubField = updateSetting.UpdateMatch.FirstValueSubField();
                            if (subField != null && valSubField != null)
                            {
                                var subFormField = subField.Field.Split('>', StringSplitOptions.RemoveEmptyEntries)[0];
                                var valSubFormFieldField = valSubField.Field.Split('>', StringSplitOptions.RemoveEmptyEntries)[0];

                                var valSubData = dataContext.NodeDatas[valSubField.NodeId!].ActionDatas.FirstOrDefault()?.FormData;
                                if (valSubData != null && valSubData.Data.ContainsKey(valSubFormFieldField))
                                {
                                    var valArrData = valSubData.Data.GetValueOrDefault<IEnumerable<object>>(valSubFormFieldField);
                                    if (valArrData != null)
                                    {
                                        for (var i = 0; i < valArrData.Count(); i++)
                                        {
                                            var subMatchExp = BuildFieldMatchExp(updateSetting.UpdateMatch, scriptData, i);

                                            foreach (var toUpdate in toUpdates)
                                            {
                                                var toUpdateSubData = toUpdate.FormData.Data.GetValueOrDefault<List<ExpandoObject>>(subFormField);
                                                if (toUpdateSubData == null)
                                                {
                                                    //如果子表单不存在
                                                    toUpdateSubData = new List<ExpandoObject>();
                                                    toUpdate.FormData.Data.AddOrUpdate(subField.Field, toUpdateSubData);
                                                }

                                                var toUpdateSubItem = toUpdateSubData?.FirstOrDefault(x => ScriptEngine.Evaluate<bool>(subMatchExp, x.ToScriptData()).Value);

                                                if (toUpdateSubItem == null)
                                                {
                                                    if (updateSetting.InsertIfNoData)
                                                    {
                                                        //添加新数据
                                                        toUpdateSubItem = new ExpandoObject();

                                                        updateSetting.FieldSettings.ForEach(x =>
                                                        {
                                                            var field = x.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries)[1];
                                                            if (x.ValueIsSubField())
                                                            {
                                                                UpdateSubToSub(toUpdateSubItem, field, x, i, scriptData);
                                                            }
                                                            else
                                                            {
                                                                UpdateMainToSub(toUpdateSubItem, field, x, scriptData);
                                                            }
                                                        });

                                                        toUpdateSubData!.Add(toUpdateSubItem);
                                                    }
                                                }
                                                else
                                                {
                                                    updateSetting.FieldSettings.ForEach(x =>
                                                    {
                                                        var field = x.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries)[1];
                                                        if (x.ValueIsSubField())
                                                        {
                                                            UpdateSubToSub(toUpdateSubItem, field, x, i, scriptData);
                                                        }
                                                        else
                                                        {
                                                            UpdateMainToSub(toUpdateSubItem, field, x, scriptData);
                                                        }
                                                    });
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            //MM -> S，此时源表为多条数据节点
                            var subField = updateSetting.UpdateMatch.FirstSubField();
                            if (subField != null)
                            {
                                var subFormField = subField.Field.Split('>', StringSplitOptions.RemoveEmptyEntries)[0];
                                var nodeDatas = dataContext.NodeDatas[multiDataFieldSetting.ValueField!.Field.NodeId!].ActionDatas;
                                for (var mi = 0; mi < nodeDatas.Count; mi++)
                                {
                                    var subMatchExp = BuildFieldMatchExp(updateSetting.UpdateMatch, scriptData, mi);

                                    foreach (var toUpdate in toUpdates)
                                    {
                                        var toUpdateSubData = toUpdate.FormData.Data.GetValueOrDefault<List<ExpandoObject>>(subFormField);
                                        if (toUpdateSubData == null)
                                        {
                                            //如果子表单不存在
                                            toUpdateSubData = new List<ExpandoObject>();
                                            toUpdate.FormData.Data.AddOrUpdate(subField.Field, toUpdateSubData);
                                        }

                                        var toUpdateSubItem = toUpdateSubData?.FirstOrDefault(x => ScriptEngine.Evaluate<bool>(subMatchExp, x.ToScriptData()).Value);

                                        if (toUpdateSubItem == null)
                                        {
                                            if (updateSetting.InsertIfNoData)
                                            {
                                                //添加新数据
                                                toUpdateSubItem = new ExpandoObject();

                                                updateSetting.InsertFieldSettings.ForEach(x =>
                                                {
                                                    if (x.Field.IsSubField)
                                                    {
                                                        var field = x.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries)[1];
                                                        if (x.ValueIsSingleResultNode())
                                                        {
                                                            UpdateMainToSub(toUpdateSubItem, field, x, scriptData);
                                                        }
                                                        else
                                                        {
                                                            UpdateMultiMainToSub(toUpdateSubItem, mi, field, x, scriptData);
                                                        }
                                                    }
                                                });

                                                toUpdateSubData!.Add(toUpdateSubItem);
                                            }
                                        }
                                        else
                                        {
                                            updateSetting.FieldSettings.ForEach(x =>
                                            {
                                                var field = x.Field.Field.Split('>', StringSplitOptions.RemoveEmptyEntries)[1];
                                                if (x.ValueIsSingleResultNode())
                                                {
                                                    UpdateMainToSub(toUpdateSubItem, field, x, scriptData);
                                                }
                                                else
                                                {
                                                    UpdateMultiMainToSub(toUpdateSubItem, mi, field, x, scriptData);
                                                }
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var multiDataFieldSetting = updateSetting.FieldSettings.FirstOrDefault(x => !x.ValueIsSingleResultNode());

                        //此处不应该为空
                        if (multiDataFieldSetting != null)
                        {
                            //MM -> M，此时源表为多条数据节点
                            var nodeDatas = dataContext.NodeDatas[multiDataFieldSetting.ValueField!.Field.NodeId!].ActionDatas;
                            for (var mi = 0; mi < nodeDatas.Count; mi++)
                            {
                                var matchExp = BuildFieldMatchExp(updateSetting.UpdateMatch, scriptData, mi);
                                var toUpdate = toUpdates.FirstOrDefault(x => ScriptEngine.Evaluate<bool>(matchExp, x.FormData.ToScriptData()).Value);
                                if (toUpdate == null)
                                {
                                    if (updateSetting.InsertIfNoData)
                                    {
                                        //添加新数据
                                        var insertData = new FormData()
                                        {
                                            AppId = dataContext.AppId,
                                            CorpId = dataContext.CorpId,
                                            FormId = formDef.Id,
                                            Data = new ExpandoObject(),
                                            CreateBy = dataContext.WfStarter,
                                            CreateTime = DateTime.Now,
                                        };

                                        //逐条填充字段
                                        InsertFormData(insertData, dataContext, scriptData, formDef, updateSetting.InsertFieldSettings, mi);

                                        actionDatas.Add(new ActionFormData { FormData = insertData, State = DataState.Inserted });
                                    }
                                }
                                else
                                {
                                    toUpdate.State = DataState.Modified;
                                    actionDatas.Add(toUpdate);

                                    updateSetting.FieldSettings.ForEach(x =>
                                    {
                                        if (!x.ValueIsSingleResultNode())
                                        {
                                            //MM->M
                                            SetMultiMainToMain(toUpdate.FormData, mi, x, scriptData);
                                        }
                                        else
                                        {
                                            //M->M
                                            SetMainToMain(toUpdate.FormData, x, scriptData);
                                        }
                                    });
                                }
                            }
                        }
                    }
                }
            }
            else if (updateSetting.InsertIfNoData)  //如果插入数据为true
            {
                if (updateSetting.InsertFieldSettings.Count > 0)
                {
                    var insertDatas = BuildInsertDatas(dataContext, formDef, updateSetting.InsertFieldSettings);
                    if (insertDatas?.Count > 0)
                    {
                        insertDatas.ForEach(x => actionDatas.Add(x));
                    }
                }
            }

            if ((actionDatas.Count > 0))
            {
                dataContext.NodeDatas.Add(Metadata!.Id, new DfNodeData
                {
                    NodeId = Metadata.Id,
                    SingleResult = Metadata.DfNodeSetting!.SingleResult,
                    FormId = updateSetting.FormId,
                    ActionDatas = actionDatas
                });
            }

            CreateExecLog(context.Workflow, dataContext, Metadata!);

            return ExecutionResult.Next();

        }

        protected string BuildFieldMatchExp(DataMatchSetting matchSetting, Dictionary<string, object>? scriptData, int mIndex = -1)
        {
            if (matchSetting.Items?.Count > 0)
            {
                List<string> subExps = new List<string>();
                matchSetting.Items.ForEach(x => subExps.Add(BuildFieldMatchExp(x, scriptData, mIndex)));

                var rel = (matchSetting.Rel == FilterRel.Or) ? "||" : "&&";
                return string.Join(rel, subExps.Select(x => $"({x})"));
            }
            else
            {
                if (string.IsNullOrEmpty(matchSetting.Field?.Field))
                    return ScriptExpression.TRUE;

                var value = EvalMatchValue(matchSetting, scriptData, mIndex);
                return FormatMatchExp(matchSetting.Field, matchSetting.Op ?? FilterOp.Eq, value);
            }
        }
        private string FormatMatchExp(FormField matchField, string op, object? value)
        {
            var field = matchField.Field;
            if (matchField.IsSubField)
                field = field.Split('>', StringSplitOptions.RemoveEmptyEntries)[1];
            else
                field = $"data.f_{matchField.FormId}.{matchField.Field}";

            var oper = ParseOp(matchField.Type, op);

            var exp = "";

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
        private string EvalMatchValue(DataMatchSetting matchSetting, Dictionary<string, object>? scriptData, int mIndex)
        {
            object? value = null;
            if (matchSetting.Value != null)
            {
                if (matchSetting.Value.Type == FieldValueType.Field)
                {
                    if (matchSetting.Value.FieldValue!.IsSubField)
                    {
                        var valueExp = $"data.n_{matchSetting.Value!.FieldValue!.NodeId}.{matchSetting.Value!.FieldValue!.Field!.Replace(">", $"[{mIndex}].")}";
                        value = ScriptEngine.Evaluate(valueExp, scriptData).Value;
                    }
                    else
                    {
                        if (mIndex > -1)
                        {
                            var valueExp = $"data.n_{matchSetting.Value!.FieldValue!.NodeId}[{mIndex}].{matchSetting.Value!.FieldValue.Field}";
                            value = ScriptEngine.Evaluate(valueExp, scriptData).Value;
                        }
                    }
                }
                else
                {
                    value = matchSetting.Value.Value;
                }
            }

            var valStr = "";
            var fType = matchSetting.Field.Type.ToLower();

            if (fType == FieldType.InputNumber)
                valStr = $"{value ?? "0"}";
            else
                valStr = $"'{value}'";

            return valStr;
        }
    }
}
