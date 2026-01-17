using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EIMSNext.Common;
using EIMSNext.Core.Query;
using EIMSNext.Entity;

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
