using System.Runtime.CompilerServices;

using EIMSNext.Entities;

namespace EIMSNext.Flow.Core.Nodes.EventFlow
{
    public static class FormFieldSettingExtensions
    {
        public static bool ValueIsSingleResultNode(this FormFieldSetting fieldSetting)
        {
            //ValueField 可能为空（Formula 未配 drivingField / Field 缺 FieldValue），按单结果处理
            return (fieldSetting.ValueType != FieldValueType.Field && fieldSetting.ValueType != FieldValueType.Formula)
                || (fieldSetting.ValueField?.SingleResultNode ?? true);
        }
        public static bool ValueIsSubField(this FormFieldSetting fieldSetting)
        {
            return (fieldSetting.ValueType == FieldValueType.Field || fieldSetting.ValueType == FieldValueType.Formula)
                && (fieldSetting.ValueField?.Field.IsSubField ?? false);
        }

        public static bool IsEmpty(this DataMatchSetting matchSetting)
        {
            return string.IsNullOrEmpty(matchSetting.Field.Field)
                && (matchSetting.Items == null || matchSetting.Items.Count == 0);
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
