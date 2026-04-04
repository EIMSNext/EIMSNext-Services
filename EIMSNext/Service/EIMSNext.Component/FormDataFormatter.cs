using System.Dynamic;
using EIMSNext.Service.Entities;

namespace EIMSNext.Component
{
    public static class FormDataFormatter
    {
        public static ExpandoObject Format(FormData data, IList<FieldDef> fieldDefs)
        {
            return new ExpandoObject();
        }
    }
}
