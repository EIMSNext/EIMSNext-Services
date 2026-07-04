using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EIMSNext.Plugin.Contracts
{
    internal static class PluginValueBinder
    {
        public static object? Deserialize(JsonObject args, Type parameterType)
        {
            var normalized = new JsonObject(args.ToDictionary(x => x.Key, x => x.Value?.DeepClone()));
            foreach (var property in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attribute = property.GetCustomAttribute<PluginInputAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                var key = ResolveKey(property, attribute.Key);
                var existingKey = normalized.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
                if (existingKey == null)
                {
                    continue;
                }

                normalized[key] = NormalizeValue(attribute.FieldType, normalized[existingKey]);
                if (!string.Equals(existingKey, key, StringComparison.Ordinal))
                {
                    normalized.Remove(existingKey);
                }
            }

            return normalized.DeserializeFromJson(parameterType);
        }

        private static JsonNode? NormalizeValue(string fieldType, JsonNode? value)
        {
            if (value == null)
            {
                return null;
            }

            return fieldType.ToLowerInvariant() switch
            {
                PluginFieldKind.SingleEmployee => ProjectObject(value, "id", "value", "label", "type"),
                PluginFieldKind.MultipleEmployee => ProjectArray(value, "id", "value", "label", "type"),
                PluginFieldKind.SingleDepartment => ProjectObject(value, "id", "value", "label", "type"),
                PluginFieldKind.MultipleDepartment => ProjectArray(value, "id", "value", "label", "type"),
                _ => value.DeepClone(),
            };
        }

        private static JsonNode? ProjectObject(JsonNode? value, params string[] keys)
        {
            if (value == null)
            {
                return null;
            }

            if (value is not JsonObject obj)
            {
                return value.DeepClone();
            }

            var result = new JsonObject();
            foreach (var key in keys)
            {
                if (obj.TryGetPropertyValue(key, out var propertyValue))
                {
                    result[key] = propertyValue?.DeepClone();
                }
            }

            return result;
        }

        private static JsonNode? ProjectArray(JsonNode? value, params string[] keys)
        {
            if (value == null)
            {
                return null;
            }

            if (value is not JsonArray array)
            {
                return value.DeepClone();
            }

            var result = new JsonArray();
            foreach (var item in array)
            {
                result.Add(ProjectObject(item, keys));
            }
            return result;
        }

        private static string ResolveKey(PropertyInfo property, string? explicitKey)
        {
            if (!string.IsNullOrWhiteSpace(explicitKey))
            {
                return explicitKey;
            }

            return char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
        }
    }
}
