using System.Runtime.CompilerServices;

using EIMSNext.Entity;

namespace EIMSNext.Flow.Core.Node.Dataflow
{
    public static class FormFieldSettingExtensions
    {
        public static bool ValueIsSingleResultNode(this FormFieldSetting fieldSetting)
        {
            return fieldSetting.ValueType != FieldValueType.Field || (fieldSetting.ValueField!.SingleResultNode ?? true);
        }
        public static bool ValueIsSubField(this FormFieldSetting fieldSetting)
        {
            return fieldSetting.ValueType == FieldValueType.Field && fieldSetting.ValueField!.Field.IsSubField;
        }

        public static bool IsEmpty(this DataMatchSetting matchSetting)
        {
            return string.IsNullOrEmpty(matchSetting.Field.Field) && matchSetting.Items?.Count == 0;
        }

        public static bool IsSubFieldMatch(this DataMatchSetting matchSetting)
        {
            return matchSetting.Field.IsSubField || (matchSetting.Items?.Any(m => m.Field.IsSubField) ?? false);
        }

        public static FormField? FirstSubField(this DataMatchSetting matchSetting)
        {
            if (matchSetting.Field.IsSubField) return matchSetting.Field;
            return matchSetting.Items?.FirstOrDefault(m => m.Field.IsSubField)?.Field;
        }
        public static FormField? FirstValueSubField(this DataMatchSetting matchSetting)
        {
            if (FieldValueType.Field == matchSetting.Value?.Type && matchSetting.Value.FieldValue!.IsSubField) return matchSetting.Value.FieldValue;
            return matchSetting.Items?.FirstOrDefault(m => FieldValueType.Field == m.Value?.Type && m.Value.FieldValue!.IsSubField)?.Value!.FieldValue;
        }
    }
}
