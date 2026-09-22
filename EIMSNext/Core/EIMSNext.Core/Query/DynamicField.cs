using EIMSNext.Common;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态字段路径工具。
    /// </summary>
    public static class DynamicField
    {
        /// <summary>
        /// 根据字段类型格式化筛选用的字段路径。
        /// </summary>
        /// <param name="field">字段路径。</param>
        /// <param name="fieldType">字段类型。</param>
        /// <returns>格式化后的字段路径。</returns>
        public static string FormatFieldForFilter(string field, string? fieldType)
        {
            var finalField = field;

            if (!string.IsNullOrEmpty(fieldType))
            {
                switch (fieldType)
                {
                    case FieldType.Select1:
                    case FieldType.Select2:
                    case FieldType.CheckBox:
                    case FieldType.Radio:
                        if (!(
                           field.EndsWith(".label") ||
                           field.EndsWith(".value")))
                        {
                            finalField = $"{field}.value";
                        }
                        break;
                    case FieldType.Employee1:
                    case FieldType.Employee2:
                    case FieldType.Department1:
                    case FieldType.Department2:
                        if (!(field.EndsWith(".id") ||
                            field.EndsWith(".value") ||
                            field.EndsWith(".label")))
                        {
                            finalField = $"{field}.id";
                        }
                        break;
                }
            }

            return finalField;
        }
    }
}
