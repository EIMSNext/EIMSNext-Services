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
    }
    public class DynamicFieldList : List<DynamicField> { }
}
