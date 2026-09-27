using System.Text.Json.Nodes;

using EIMSNext.Common;

namespace EIMSNext.Component
{
    public static class FormRelatedSourceResolver
    {
        private static readonly HashSet<string> RemoteOptionTypes =
        [
            FieldType.Select1,
            FieldType.Select2,
        ];

        public static IReadOnlyCollection<string> ResolveFormIds(string? layout)
        {
            if (string.IsNullOrWhiteSpace(layout))
            {
                return [];
            }

            try
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Visit(JsonNode.Parse(layout), result);
                return result.ToList();
            }
            catch
            {
                return [];
            }
        }

        private static void Visit(JsonNode? node, ISet<string> result)
        {
            if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    Visit(item, result);
                }
                return;
            }

            if (node is not JsonObject obj)
            {
                return;
            }

            var type = GetString(obj, "type");
            if (string.Equals(type, FieldType.DataSelect, StringComparison.OrdinalIgnoreCase) &&
                obj["props"] is JsonObject props)
            {
                Add(result, GetString(props, "dataSource"));
            }

            if (!string.IsNullOrWhiteSpace(type) &&
                RemoteOptionTypes.Contains(type) &&
                obj["effect"] is JsonObject effect &&
                effect["source"] is JsonObject source)
            {
                Add(result, GetString(source, "formId"));
                Add(result, GetNestedString(source, "label", "formId"));
                Add(result, GetNestedString(source, "value", "formId"));
            }

            foreach (var property in obj)
            {
                Visit(property.Value, result);
            }
        }

        private static string? GetNestedString(JsonObject obj, string objectName, string propertyName)
        {
            return obj[objectName] is JsonObject nested ? GetString(nested, propertyName) : null;
        }

        private static string? GetString(JsonObject obj, string propertyName)
        {
            return obj[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
                ? text
                : null;
        }

        private static void Add(ISet<string> result, string? formId)
        {
            if (!string.IsNullOrWhiteSpace(formId))
            {
                result.Add(formId);
            }
        }
    }
}
