using EIMSNext.Common;
using EIMSNext.Core.Query;
using EIMSNext.Scripting;
using EIMSNext.Service.Entities;

namespace EIMSNext.Component
{
    public class ConditionList
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
                filter.Field = Fields.IsSystemField(Field.Field) ? Field.Field : "data." + Field.Field;
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

        public string ToScriptExpression()
        {
            if (Items != null && Items.Count > 0)
            {
                List<string> subExps = new List<string>();
                Items.ForEach(x => subExps.Add(x.ToScriptExpression()));

                var rel = (Rel == FilterRel.Or) ? "||" : "&&";
                return string.Join(rel, subExps.Select(x => $"({x})"));
            }
            else
            {
                if (string.IsNullOrEmpty(Field?.Field))
                    return ScriptExpression.TRUE;

                return FormatExp(Field, Op ?? FilterOp.Eq, Value);
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
    }

    public class ConditionValue
    {
        public string? Type { get; set; }
        public object? Value { get; set; }
        public FormFieldDef? FieldValue { get; set; }
    }

    public class FormFieldDef
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
}
