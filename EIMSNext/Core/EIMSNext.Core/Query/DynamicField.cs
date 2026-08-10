using EIMSNext.Common;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态查询投影字段。
    /// </summary>
    public class DynamicField
    {
        public DynamicField() { }
        public DynamicField(string field, bool visible = true)
        {
            Field = field;
            Visible = visible;
        }

        /// <summary>字段路径。</summary>
        public string Field { get; set; } = "";
        /// <summary>是否返回该字段。</summary>
        public bool Visible { get; set; } = true;

        public static DynamicField Create(string field, bool visible = true)
        {
            return new DynamicField(field, visible);
        }
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
    public class DynamicFieldList : List<DynamicField> { }
}
