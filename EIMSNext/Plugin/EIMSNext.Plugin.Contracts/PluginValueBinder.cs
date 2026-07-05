using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EIMSNext.Plugin.Contracts
{
    internal static class PluginValueBinder
    {
        public static object? Deserialize(JsonObject args, Type parameterType)
        {
            if (!typeof(IPluginField).IsAssignableFrom(parameterType))
            {
                throw new InvalidOperationException($"Plugin argument [{parameterType.Name}] must implement IPluginField.");
            }

            var normalized = new JsonObject(args.ToDictionary(x => x.Key, x => x.Value?.DeepClone()));
            NormalizeObject(normalized, parameterType);
            return normalized.DeserializeFromJson(parameterType);
        }

        private static void NormalizeObject(JsonObject normalized, Type objectType)
        {
            foreach (var property in objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attribute = property.GetCustomAttribute<PluginInputAttribute>();
                var subListAttribute = property.GetCustomAttribute<PluginSubListAttribute>();
                if (attribute != null && subListAttribute != null)
                {
                    throw new InvalidOperationException(
                        $"Plugin field [{property.Name}] cannot use PluginInput and PluginSubList together.");
                }

                if (attribute != null)
                {
                    NormalizeInputField(normalized, property, attribute);
                }
                else if (subListAttribute != null)
                {
                    NormalizeSubListField(normalized, property, subListAttribute);
                }
            }
        }

        private static void NormalizeInputField(JsonObject normalized, PropertyInfo property, PluginInputAttribute attribute)
        {
            if (string.Equals(attribute.FieldType, PluginFieldKind.TableForm, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Plugin field [{property.Name}] must use PluginSubList instead of PluginFieldKind.TableForm.");
            }

            var key = ResolveKey(property, attribute.Key);
            var existingKey = normalized.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
            if (existingKey == null)
            {
                return;
            }

            normalized[key] = NormalizeValue(attribute.FieldType, normalized[existingKey]);
            if (!string.Equals(existingKey, key, StringComparison.Ordinal))
            {
                normalized.Remove(existingKey);
            }
        }

        private static void NormalizeSubListField(JsonObject normalized, PropertyInfo property, PluginSubListAttribute attribute)
        {
            var key = ResolveKey(property, attribute.Key);
            var existingKey = normalized.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
            if (existingKey == null)
            {
                return;
            }

            var itemType = GetSingleEnumerableItemType(property.PropertyType);
            if (itemType == null)
            {
                throw new InvalidOperationException($"Plugin sub list field [{property.Name}] must be a generic list.");
            }

            normalized[key] = NormalizeSubListValue(normalized[existingKey], itemType);
            if (!string.Equals(existingKey, key, StringComparison.Ordinal))
            {
                normalized.Remove(existingKey);
            }
        }

        private static JsonNode? NormalizeSubListValue(JsonNode? value, Type itemType)
        {
            if (value is not JsonArray array)
            {
                return value?.DeepClone();
            }

            var result = new JsonArray();
            foreach (var item in array)
            {
                if (item is JsonObject obj)
                {
                    var row = new JsonObject(obj.ToDictionary(x => x.Key, x => x.Value?.DeepClone()));
                    NormalizeObject(row, itemType);
                    result.Add(row);
                }
                else
                {
                    result.Add(item?.DeepClone());
                }
            }

            return result;
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

        private static Type? GetSingleEnumerableItemType(Type type)
        {
            if (type == typeof(string))
            {
                return null;
            }

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType && type.GetGenericArguments().Length == 1)
            {
                return type.GetGenericArguments()[0];
            }

            return type.GetInterfaces()
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(x => x.GetGenericArguments()[0])
                .Distinct()
                .SingleOrDefault();
        }
    }
}
