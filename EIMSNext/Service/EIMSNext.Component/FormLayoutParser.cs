using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using EIMSNext.Common;
using EIMSNext.Common.Extension;
using EIMSNext.Core.Query;
using EIMSNext.Entity;

namespace EIMSNext.Component
{
    public class FormLayoutParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="layout"></param>
        /// <returns></returns>
        public IList<FieldDef> Parse(string layout)
        {
            var fieldList = new List<FieldDef>();
            var fieldArr = layout.DeserializeFromJson<JsonArray>();

            fieldList.AddRange(ParseChildren(fieldArr));

            return fieldList;
        }
        private IList<FieldDef> ParseChildren(JsonArray? fieldArr)
        {
            var fieldList = new List<FieldDef>();

            if (fieldArr == null || fieldArr.Count == 0) return fieldList;

            if (fieldArr == null || fieldArr.Count == 0)
                return fieldList;

            foreach (JsonObject? field in fieldArr)
            {
                if (field == null) continue;

                if (field.ContainsKey("type"))
                {
                    //因为FieldType定义中不包括 Layout的类型，所以此处都是表单控件和子表单控件
                    var fieldDef = ParseField(field)!;
                    var fType = field["type"]!.GetValue<string>();


                    if (field.ContainsKey("props"))
                    {
                        var props = field["props"]!.AsObject();

                        switch (fType)
                        {
                            case FieldType.TableForm:
                                {
                                    //解析Columns
                                    if (props.ContainsKey("columns"))
                                    {
                                        fieldDef.Columns = new List<FieldDef>();
                                        var columns = props["columns"]!.AsArray();
                                        foreach (var column in columns)
                                        {
                                            var subDef = ParseField(column?["rule"]?.AsArray()?.FirstOrDefault()?.AsObject());
                                            if (subDef != null)
                                                fieldDef.Columns.Add(subDef);
                                        }
                                    }
                                }
                                break;
                            case FieldType.TimeStamp:
                                {
                                    fieldDef.Options.Format = props["format"]?.GetValue<string>();
                                }
                                break;
                        }
                    }

                    fieldList.Add(fieldDef);
                }
                else
                {
                    //Layout控件
                    if (field.ContainsKey("children"))
                    {
                        fieldList.AddRange(ParseChildren(field["children"]!.AsArray()));
                    }
                }
            }

            return fieldList;
        }
        private FieldDef? ParseField(JsonObject? field)
        {
            if (field == null) return null;

            var fieldDef = new FieldDef();
            fieldDef.Type = field["type"]!.GetValue<string>();
            fieldDef.Field = field["field"]!.GetValue<string>();
            fieldDef.Title = field["title"]!.GetValue<string>();

            var computed = field["computed"]?.AsObject();
            if (computed != null)
            {
                fieldDef.Options.ValueOpt = new ValueOpt() { Formula = computed["value"]?.GetValue<string>() };
                if (!string.IsNullOrEmpty(fieldDef.Options.ValueOpt.Formula))
                {
                    fieldDef.Options.ValueOpt.Depends = ParseDepends(fieldDef.Options.ValueOpt.Formula);
                }
            }
            return fieldDef;
        }

        private string? ParseDepends(string? formula)
        {
            if (string.IsNullOrEmpty(formula)) return null;

            //字段为自定义的nanoid, 格式j+15位随机数
            string pattern = @"\bj[a-z0-9]{15}\b";
            IEnumerable<string> depends = Regex.Matches(formula, pattern).Select(m => m.Value);

            return depends.Any() ? string.Join(',', depends.Distinct()) : null;
        }
    }
}
