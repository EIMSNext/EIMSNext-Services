using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Common.Extension
{
    public static class ExpandoObjectExtension
    {
        public static bool ContainsKey(this ExpandoObject obj, string prop)
        {
            return (obj as IDictionary<string, object?>).ContainsKey(prop);
        }
    }
}
