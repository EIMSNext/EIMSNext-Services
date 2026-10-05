using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using EIMSNext.Common;
using EIMSNext.Entities;

namespace EIMSNext.Component
{
    public class FormLayoutParser
    {
        public IList<FieldDef> Parse(string layout)
        {
            var fieldArr = ParseLayout(layout);
            var fieldList = ParseChildren(fieldArr);
            PopulateDepends(fieldList);

            return fieldList;
        }

        private static JsonArray? ParseLayout(string layout)
        {
            if (string.IsNullOrWhiteSpace(layout))
            {
                return null;
            }

            try
            {
                return JsonNode.Parse(layout) as JsonArray;
            }
            catch
            {
                return null;
            }
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
                var type = GetStringValue(field, "type");
                if (!string.IsNullOrWhiteSpace(type) && FieldType.IsInputField(type))
                {
                    var fieldDef = ParseField(field);
                    if (fieldDef != null)
                    {
                        fieldList.Add(fieldDef);
                    }
                }
                else if (field.TryGetPropertyValue("children", out var childrenNode) && childrenNode is JsonArray children)
                {
                    fieldList.AddRange(ParseChildren(children));
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
                Type = GetStringValue(field, "type") ?? string.Empty,
                Field = GetStringValue(field, "field") ?? string.Empty,
                Title = GetStringValue(field, "title") ?? string.Empty,
                Hidden = field["hidden"]?.GetValue<bool>() ?? false,
                Source = GetStringValue(field, "source"),
                SystemKind = GetStringValue(field, "systemKind"),
                Required = field["$required"]?.GetValue<bool>() ?? false,
            };

            var fieldType = fieldDef.Type;
            if (field.TryGetPropertyValue("props", out var propsNode) && propsNode is JsonObject props)
            {
                if (props.TryGetPropertyValue("required", out var requiredNode) && requiredNode is JsonValue requiredValue)
                {
                    fieldDef.Props.Required = requiredValue.GetValue<bool>();
                }

                if (fieldType is FieldType.Employee1 or FieldType.Employee2 or FieldType.Department1 or FieldType.Department2)
                {
                    fieldDef.Props.MemberSource = ParseMemberSource(props, fieldType);
                }

                switch (fieldType)
                {
                    case FieldType.TableForm:
                        if (props.TryGetPropertyValue("columns", out var columnsNode) && columnsNode is JsonArray columns)
                        {
                            fieldDef.Columns = new List<FieldDef>();
                            foreach (var column in columns)
                            {
                                if (column is not JsonObject columnObj ||
                                    !columnObj.TryGetPropertyValue("rule", out var ruleNode) ||
                                    ruleNode is not JsonArray ruleArray ||
                                    ruleArray.FirstOrDefault() is not JsonObject rule)
                                {
                                    continue;
                                }

                                var subDef = ParseField(rule);
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

            if (field.TryGetPropertyValue("options", out var optionsNode) && optionsNode is JsonArray options)
            {
                fieldDef.Props.Options = options.SerializeToJson().DeserializeFromJson<List<ValueOption>>();
            }

            if (field.TryGetPropertyValue("computed", out var computedNode) && computedNode is JsonObject computed)
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

        private static MemberSource ParseMemberSource(JsonObject props, string fieldType)
        {
            var mode = GetStringValue(props, "limitType")?.ToLowerInvariant() == MemberSourceMode.Custom
                ? MemberSourceMode.Custom
                : MemberSourceMode.All;
            var source = new MemberSource { Mode = mode };

            if (props["memberSource"] is JsonObject memberSource)
            {
                var parsed = memberSource.SerializeToJson().DeserializeFromJson<MemberSource>();
                if (parsed != null)
                {
                    source.Mode = string.Equals(parsed.Mode, MemberSourceMode.Custom, StringComparison.OrdinalIgnoreCase)
                        ? MemberSourceMode.Custom
                        : MemberSourceMode.All;
                    source.Items = NormalizeMemberSourceItems(parsed.Items, fieldType);
                    return source;
                }
            }

            if (props["limitScope"] is not JsonArray scope)
            {
                return source;
            }

            var items = new List<MemberSourceItem>();
            foreach (var node in scope)
            {
                if (node is not JsonObject item)
                {
                    continue;
                }

                var id = GetStringValue(item, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var type = GetStringValue(item, "type")?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(type) || type is "1" or "2" or "3" or "4")
                {
                    type = GetMemberSourceType(item["type"]);
                }
                if (type is "dynamic" or "dynamicparam")
                {
                    items.Add(new MemberSourceItem { Type = "dynamic", Id = id.ToLowerInvariant() });
                    continue;
                }

                var sourceType = type switch
                {
                    "department" => "department",
                    "employeegroup" => "employeeGroup",
                    "employee" => "employee",
                    _ => fieldType is FieldType.Department1 or FieldType.Department2 ? "department" : null,
                };
                if (sourceType == null)
                {
                    continue;
                }

                items.Add(new MemberSourceItem
                {
                    Type = sourceType,
                    Id = id,
                    Cascaded = item["cascadedDept"]?.GetValue<bool>() ?? item["cascaded"]?.GetValue<bool>() ?? false,
                });
            }

            source.Items = NormalizeMemberSourceItems(items, fieldType);
            return source;
        }

        private static IList<MemberSourceItem> NormalizeMemberSourceItems(IEnumerable<MemberSourceItem>? items, string fieldType)
        {
            var isDepartmentField = fieldType is FieldType.Department1 or FieldType.Department2;
            var result = new List<MemberSourceItem>();
            foreach (var item in items ?? [])
            {
                var type = item.Type?.Trim().ToLowerInvariant() switch
                {
                    "department" => "department",
                    "employeegroup" => "employeeGroup",
                    "employee" => "employee",
                    "dynamic" => "dynamic",
                    _ => null,
                };
                var id = item.Id?.Trim();
                if (type == null || string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (type == "dynamic")
                {
                    id = id.ToLowerInvariant();
                    if (id != "curuser" && id != "curdept")
                    {
                        continue;
                    }

                    if (id == "curuser" && isDepartmentField)
                    {
                        continue;
                    }
                }

                if (type == "employeeGroup" && isDepartmentField)
                {
                    continue;
                }

                if (!result.Any(x => x.Type == type && x.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && x.Cascaded == item.Cascaded))
                {
                    result.Add(new MemberSourceItem { Type = type, Id = id, Cascaded = item.Cascaded });
                }
            }

            return result;
        }

        private static string? GetMemberSourceType(JsonNode? node)
        {
            if (node is not JsonValue)
            {
                return null;
            }

            var raw = node.ToJsonString().Trim('"');
            if (!int.TryParse(raw, out var type))
            {
                return null;
            }

            return type switch
            {
                1 => "department",
                2 => "employee",
                3 => "employeeGroup",
                4 => "dynamic",
                _ => null,
            };
        }

        private static string? GetStringValue(JsonObject field, string propertyName)
        {
            if (!field.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            {
                return null;
            }

            try
            {
                return value.GetValue<string>();
            }
            catch
            {
                return null;
            }
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
