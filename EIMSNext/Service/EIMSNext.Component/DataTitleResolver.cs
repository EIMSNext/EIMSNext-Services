using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;

using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Service.Entities;

namespace EIMSNext.Component
{
    public class DataTitleResolver
    {
        private static readonly Regex TokenRegex = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

        public string ResolveDataTitle(FormData data, FormDef formDef)
        {
            var settings = formDef.FormSettings?.Advanced?.DataTitle;
            if (settings == null || string.Equals(settings.Mode, "default", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveDefaultTitle(data, formDef);
            }

            return Resolve(settings.Content, data, formDef);
        }

        public string Resolve(string? template, FormData data, FormDef formDef)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            var fields = formDef.Content.Items ?? [];
            var formatted = (IDictionary<string, object?>)FormDataFormatter.FormatForDisplay(data, fields);

            // 补齐系统字段，供标题/通知模板直接复用。
            formatted[Fields.CreateBy] = data.CreateBy?.Label ?? string.Empty;
            formatted[Fields.CreateTime] = data.CreateTime > 0
                ? FormDataFormatter.Format(data, fields).AsDictionaryValue(Fields.CreateTime)
                : string.Empty;
            formatted[Fields.UpdateTime] = data.UpdateTime.HasValue
                ? FormDataFormatter.Format(data, fields).AsDictionaryValue(Fields.UpdateTime)
                : string.Empty;
            formatted[Fields.FlowStatus] = data.FlowStatus.ToString();

            return TokenRegex.Replace(template, match =>
            {
                var fieldPath = match.Groups[1].Value;
                var value = ResolveFieldValue(formatted, fieldPath);
                return value ?? string.Empty;
            });
        }

        private static string ResolveDefaultTitle(FormData data, FormDef formDef)
        {
            var firstField = (formDef.Content.Items ?? [])
                .FirstOrDefault(x => !x.Hidden && !string.Equals(x.Type, FieldType.TableForm, StringComparison.OrdinalIgnoreCase));

            if (firstField == null)
            {
                return string.Empty;
            }

            return ResolveFieldValue(
                (IDictionary<string, object?>)FormDataFormatter.FormatForDisplay(data, formDef.Content.Items ?? []),
                firstField.Field) ?? string.Empty;
        }

        private static string? ResolveFieldValue(IDictionary<string, object?> formatted, string fieldPath)
        {
            if (formatted.TryGetValue(fieldPath, out var directValue))
            {
                return ToDisplayString(directValue);
            }

            if (!fieldPath.Contains('>'))
            {
                return string.Empty;
            }

            var parts = fieldPath.Split('>', 2);
            if (parts.Length != 2 || !formatted.TryGetValue(parts[0], out var tableValue) || tableValue == null)
            {
                return string.Empty;
            }

            if (tableValue is IEnumerable rows && tableValue is not string)
            {
                var values = new List<string>();
                foreach (var row in rows)
                {
                    if (row is IDictionary<string, object?> dict && dict.TryGetValue(parts[1], out var cellValue))
                    {
                        var display = ToDisplayString(cellValue);
                        if (!string.IsNullOrWhiteSpace(display))
                        {
                            values.Add(display);
                        }
                    }
                }

                return string.Join(", ", values);
            }

            return string.Empty;
        }

        private static string ToDisplayString(object? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value switch
            {
                string text => text,
                IEnumerable list when value is not string => JoinList(list),
                _ => value.ToString() ?? string.Empty,
            };
        }

        private static string JoinList(IEnumerable list)
        {
            var values = new List<string>();
            foreach (var item in list)
            {
                if (item == null)
                {
                    continue;
                }

                if (item is IDictionary<string, object?> dict)
                {
                    values.Add(string.Join(" ", dict.Values.Where(x => x != null).Select(x => x!.ToString())));
                    continue;
                }

                values.Add(item.ToString() ?? string.Empty);
            }

            return string.Join(", ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }
}
