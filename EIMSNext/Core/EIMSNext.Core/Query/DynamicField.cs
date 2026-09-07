using EIMSNext.Common;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态查询投影字段。
    /// </summary>
    public class DynamicField
    {
        /// <summary>
        /// 初始化 <see cref="DynamicField"/> 类的新实例。
        /// </summary>
        public DynamicField() { }

        /// <summary>
        /// 使用指定字段路径与可见性初始化 <see cref="DynamicField"/> 类的新实例。
        /// </summary>
        /// <param name="field">字段路径。</param>
        /// <param name="visible">是否返回该字段。</param>
        public DynamicField(string field, bool visible = true)
        {
            Field = field;
            Visible = visible;
        }

        /// <summary>字段路径。</summary>
        public string Field { get; set; } = "";
        /// <summary>是否返回该字段。</summary>
        public bool Visible { get; set; } = true;

        /// <summary>
        /// 创建动态查询投影字段。
        /// </summary>
        /// <param name="field">字段路径。</param>
        /// <param name="visible">是否返回该字段。</param>
        /// <returns>动态查询投影字段。</returns>
        public static DynamicField Create(string field, bool visible = true)
        {
            return new DynamicField(field, visible);
        }
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
    /// <summary>
    /// 动态查询投影字段列表。
    /// </summary>
    public class DynamicFieldList : List<DynamicField> { }
}
