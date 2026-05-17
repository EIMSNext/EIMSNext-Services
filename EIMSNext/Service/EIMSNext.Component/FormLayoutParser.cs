using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using EIMSNext.Common;
using EIMSNext.Service.Entities;

namespace EIMSNext.Component
{
    public class FormLayoutParser
    {
        public IList<FieldDef> Parse(string layout)
        {
            var fieldArr = layout.DeserializeFromJson<JsonArray>();
            var fieldList = ParseChildren(fieldArr);
            PopulateDepends(fieldList);

            return fieldList;
        }

        private IList<FieldDef> ParseChildren(JsonArray? fieldArr)
        {
            var fieldList = new List<FieldDef>();
            if (fieldArr == null || fieldArr.Count == 0)
            {
                return fieldList;
            }

            foreach (JsonNode? node in fieldArr)
            {
                if (node == null || node.GetValueKind() != JsonValueKind.Object)
                {
                    continue;
                }

                var field = node.AsObject();
                if (field.ContainsKey("type") && FieldType.IsInputField(field["type"]!.GetValue<string>()))
                {
                    var fieldDef = ParseField(field);
                    if (fieldDef != null)
                    {
                        fieldList.Add(fieldDef);
                    }
                }
                else if (field.ContainsKey("children"))
                {
                    fieldList.AddRange(ParseChildren(field["children"]!.AsArray()));
                }
            }

            return fieldList;
        }

        private FieldDef? ParseField(JsonObject? field)
        {
            if (field == null)
            {
                return null;
            }

            var fieldDef = new FieldDef
            {
                Type = field["type"]!.GetValue<string>(),
                Field = field["field"]!.GetValue<string>(),
                Title = field["title"]!.GetValue<string>(),
                Hidden = field["hidden"]?.GetValue<bool>() ?? false,
            };

            var fieldType = fieldDef.Type;
            if (field.ContainsKey("props"))
            {
                var props = field["props"]!.AsObject();
                switch (fieldType)
                {
                    case FieldType.TableForm:
                        if (props.ContainsKey("columns"))
                        {
                            fieldDef.Columns = new List<FieldDef>();
                            var columns = props["columns"]!.AsArray();
                            foreach (var column in columns)
                            {
                                var subDef = ParseField(column?["rule"]?.AsArray()?.FirstOrDefault()?.AsObject());
                                if (subDef != null)
                                {
                                    fieldDef.Columns.Add(subDef);
                                }
                            }
                        }
                        break;
                    case FieldType.TimeStamp:
                        fieldDef.Props.Format = props["format"]?.GetValue<string>();
                        break;
                }
            }

            if (field.ContainsKey("options"))
            {
                var options = field["options"]?.AsArray();
                if (options != null)
                {
                    fieldDef.Props.Options = options.SerializeToJson().DeserializeFromJson<List<ValueOption>>();
                }
            }

            var computed = field["computed"]?.AsObject();
            if (computed != null)
            {
                var valueNode = computed["value"];
                string? formula = null;
                if (valueNode != null)
                {
                    formula = valueNode is JsonValue jsonValue
                        ? jsonValue.GetValue<string>()
                        : valueNode.SerializeToJson();
                }

                fieldDef.Props.ValueProp = new ValueProp { Formula = formula };
            }

            return fieldDef;
        }

        private void PopulateDepends(IList<FieldDef> fields)
        {
            var fieldMap = BuildFieldMap(fields);
            foreach (var field in fields)
            {
                PopulateDepends(field, fieldMap);
            }
        }

        private void PopulateDepends(FieldDef field, IReadOnlyDictionary<string, string> fieldMap)
        {
            if (!string.IsNullOrWhiteSpace(field.Props.ValueProp?.Formula))
            {
                field.Props.ValueProp.Depends = ParseDepends(field.Props.ValueProp.Formula, fieldMap);
            }

            if (field.Columns == null || field.Columns.Count == 0)
            {
                return;
            }

            foreach (var column in field.Columns)
            {
                PopulateDepends(column, fieldMap);
            }
        }

        private Dictionary<string, string> BuildFieldMap(IList<FieldDef> fields)
        {
            var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
            {
                if (!string.IsNullOrWhiteSpace(field.Field))
                {
                    fieldMap.TryAdd(field.Field, field.Field);
                }

                if (field.Columns == null || field.Columns.Count == 0)
                {
                    continue;
                }

                foreach (var column in field.Columns)
                {
                    if (string.IsNullOrWhiteSpace(column.Field))
                    {
                        continue;
                    }

                    fieldMap.TryAdd(column.Field, column.Field);
                    fieldMap.TryAdd($"{field.Field}.{column.Field}", $"{field.Field}>{column.Field}");
                }
            }

            return fieldMap;
        }

        private string? ParseDepends(string? formula, IReadOnlyDictionary<string, string> fieldMap)
        {
            if (string.IsNullOrWhiteSpace(formula) || fieldMap.Count == 0)
            {
                return null;
            }

            var depends = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var workingFormula = formula;

            foreach (Match match in Regex.Matches(formula, "['\"]([^'\"]+)['\"]"))
            {
                var token = match.Groups[1].Value;
                if (fieldMap.TryGetValue(token, out var mappedField))
                {
                    depends.Add(mappedField);
                    workingFormula = ReplaceRangeWithWhitespace(workingFormula, match.Index, match.Length);
                }
            }

            foreach (var entry in fieldMap.OrderByDescending(x => x.Key.Length))
            {
                var escaped = Regex.Escape(entry.Key);
                var regex = new Regex($@"(?<![a-zA-Z0-9_]){escaped}(?![a-zA-Z0-9_])");
                var match = regex.Match(workingFormula);
                if (match.Success)
                {
                    depends.Add(entry.Value);
                    workingFormula = ReplaceRangeWithWhitespace(workingFormula, match.Index, match.Length);
                }
            }

            return depends.Count > 0 ? string.Join(',', depends) : null;
        }

        private static string ReplaceRangeWithWhitespace(string value, int index, int length)
        {
            if (index < 0 || length <= 0 || index >= value.Length)
            {
                return value;
            }

            var chars = value.ToCharArray();
            var max = Math.Min(index + length, chars.Length);
            for (var i = index; i < max; i++)
            {
                chars[i] = ' ';
            }

            return new string(chars);
        }
    }
}
