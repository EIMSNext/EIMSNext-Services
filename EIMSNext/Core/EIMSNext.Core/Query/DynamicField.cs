using EIMSNext.Common;

namespace EIMSNext.Core.Query
{
    public class DynamicField
    {
        public DynamicField() { }
        public DynamicField(string field, bool visible = true)
        {
            Field = field;
            Visible = visible;
        }

        public string Field { get; set; } = "";
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
                           field.EndsWith(".value") ||
                           field.EndsWith(".label")))
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
