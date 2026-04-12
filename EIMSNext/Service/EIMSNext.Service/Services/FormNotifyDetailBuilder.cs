using EIMSNext.Component;
using EIMSNext.Core.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service
{
    public class FormNotifyDetailBuilder : IFormNotifyDetailBuilder
    {
        public string BuildForAdd(FormData data, FormDef formDef)
        {
            var fields = formDef.Content.Items ?? [];
            var formatted = (IDictionary<string, object?>)FormDataFormatter.FormatForDisplay(data, fields);
            var lines = new List<string>();

            foreach (var field in fields)
            {
                if (lines.Count >= 5)
                {
                    break;
                }

                if (field.Hidden || field.Type == EIMSNext.Common.FieldType.TableForm)
                {
                    continue;
                }

                if (!formatted.TryGetValue(field.Field, out var value) || string.IsNullOrWhiteSpace(value?.ToString()))
                {
                    continue;
                }

                lines.Add($"{field.Title}: {value}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public string BuildForChange(FormData oldData, FormData newData, FormDef formDef)
        {
            var fields = formDef.Content.Items ?? [];
            var changedFields = ExpandoComparer.Compare(oldData.Data, newData.Data)
                .Select(x => x.FieldId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var oldFormatted = (IDictionary<string, object?>)FormDataFormatter.FormatForDisplay(oldData, fields);
            var newFormatted = (IDictionary<string, object?>)FormDataFormatter.FormatForDisplay(newData, fields);
            var lines = new List<string>();

            foreach (var field in fields)
            {
                if (lines.Count >= 5)
                {
                    break;
                }

                if (field.Hidden || !changedFields.Contains(field.Field))
                {
                    continue;
                }

                if (field.Type == EIMSNext.Common.FieldType.TableForm)
                {
                    lines.Add($"{field.Title}: 已修改");
                    continue;
                }

                oldFormatted.TryGetValue(field.Field, out var oldValue);
                newFormatted.TryGetValue(field.Field, out var newValue);
                lines.Add($"{field.Title}: {oldValue} -> {newValue}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
