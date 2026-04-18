using System.Dynamic;

namespace EIMSNext.Common.Extensions
{
    public static class ExpandoObjectExtension
    {
        public static bool ContainsKey(this ExpandoObject obj, string prop)
        {
            return (obj as IDictionary<string, object?>).ContainsKey(prop);
        }
    }
}
