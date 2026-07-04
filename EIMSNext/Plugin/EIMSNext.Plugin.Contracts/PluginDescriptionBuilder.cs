using System.Collections;
using System.Reflection;

namespace EIMSNext.Plugin.Contracts
{
    internal static class PluginDescriptionBuilder
    {
        private static readonly BindingFlags PluginMethodFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static PluginDesc Build(Type pluginType)
        {
            var pluginAttribute = pluginType.GetCustomAttribute<PluginAttribute>();
            var desc = new PluginDesc
            {
                Id = pluginAttribute?.Id ?? string.Empty,
                Name = pluginAttribute?.Name ?? pluginType.Name,
                Version = pluginAttribute?.Version ?? string.Empty,
                Description = pluginAttribute?.Description,
            };

            foreach (var method in GetPluginFunctions(pluginType))
            {
                var functionAttribute = method.GetCustomAttribute<PluginFunctionAttribute>()!;
                var function = new FunctionDesc
                {
                    Id = functionAttribute.Id,
                    Name = functionAttribute.Name,
                    Description = functionAttribute.Description,
                };

                foreach (var inputField in BuildInputFields(method))
                {
                    function.InputFields.Add(inputField);
                }

                foreach (var resultField in BuildOutputFields(method))
                {
                    function.ResultFields.Add(resultField);
                }

                desc.Functions.Add(function);
            }

            return desc;
        }

        public static MethodInfo? FindFunction(Type pluginType, string functionId)
        {
            var methods = pluginType.GetMethods(PluginMethodFlags);
            return methods.FirstOrDefault(method =>
                string.Equals(method.GetCustomAttribute<PluginFunctionAttribute>()?.Id, functionId, StringComparison.OrdinalIgnoreCase))
                ?? pluginType.GetMethod(functionId, PluginMethodFlags | BindingFlags.IgnoreCase);
        }

        public static object? ProjectResult(MethodInfo method, object? value)
        {
            if (value == null)
            {
                return null;
            }

            var fields = BuildOutputPropertyMap(method).ToList();
            if (fields.Count == 0)
            {
                return value;
            }

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (property, attribute) in fields)
            {
                result[ResolveKey(property, attribute.Key)] = property.GetValue(value);
            }

            return result;
        }

        private static IEnumerable<MethodInfo> GetPluginFunctions(Type pluginType)
        {
            return pluginType.GetMethods(PluginMethodFlags)
                .Where(method => !method.IsSpecialName && method.GetCustomAttribute<PluginFunctionAttribute>() != null)
                .OrderBy(method => method.MetadataToken);
        }

        private static IEnumerable<PluginFieldDesc> BuildInputFields(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                throw new InvalidOperationException($"Plugin function [{method.Name}] must declare exactly one argument.");
            }

            foreach (var property in GetFieldProperties(parameters[0].ParameterType))
            {
                var attribute = property.GetCustomAttribute<PluginInputAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                ValidateInputType(method, property, attribute.FieldType);
                var field = new PluginFieldDesc
                {
                    Key = ResolveKey(property, attribute.Key),
                    Name = attribute.Name,
                    FieldType = attribute.FieldType,
                    Required = attribute.Required,
                    AllowCustomValue = attribute.AllowCustomValue,
                    AllowFieldMapping = attribute.AllowFieldMapping,
                    Multiple = IsMultipleField(attribute.FieldType, property.PropertyType),
                    Description = attribute.Description,
                };

                foreach (var compatibleFieldType in attribute.CompatibleFieldTypes)
                {
                    field.CompatibleFieldTypes.Add(compatibleFieldType);
                }

                yield return field;
            }
        }

        private static IEnumerable<PluginResultFieldDesc> BuildOutputFields(MethodInfo method)
        {
            foreach (var (property, attribute) in BuildOutputPropertyMap(method))
            {
                ValidateOutputType(method, property, attribute.FieldType);
                yield return new PluginResultFieldDesc
                {
                    Key = ResolveKey(property, attribute.Key),
                    Name = attribute.Name,
                    FieldType = attribute.FieldType,
                    Multiple = IsMultipleField(attribute.FieldType, property.PropertyType),
                    Description = attribute.Description,
                };
            }
        }

        private static IEnumerable<(PropertyInfo Property, PluginOutputAttribute Attribute)> BuildOutputPropertyMap(MethodInfo method)
        {
            if (method.ReturnType == typeof(void))
            {
                yield break;
            }

            foreach (var property in GetFieldProperties(method.ReturnType))
            {
                var attribute = property.GetCustomAttribute<PluginOutputAttribute>();
                if (attribute != null)
                {
                    yield return (property, attribute);
                }
            }
        }

        private static IEnumerable<PropertyInfo> GetFieldProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetMethod != null)
                .OrderBy(property => property.MetadataToken);
        }

        private static void ValidateInputType(MethodInfo method, PropertyInfo property, string fieldType)
        {
            if (!IsValidClrType(fieldType, property.PropertyType))
            {
                throw new InvalidOperationException(
                    $"Plugin function [{method.Name}] input [{property.Name}] does not match field type [{fieldType}].");
            }
        }

        private static void ValidateOutputType(MethodInfo method, PropertyInfo property, string fieldType)
        {
            if (!IsValidClrType(fieldType, property.PropertyType))
            {
                throw new InvalidOperationException(
                    $"Plugin function [{method.Name}] output [{property.Name}] does not match field type [{fieldType}].");
            }
        }

        private static bool IsValidClrType(string fieldType, Type propertyType)
        {
            var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            return fieldType.ToLowerInvariant() switch
            {
                PluginFieldKind.Text or PluginFieldKind.TextArea or PluginFieldKind.SerialNo => type == typeof(string),
                PluginFieldKind.Number => IsNumericType(type),
                PluginFieldKind.Timestamp => IsTimestampType(type),
                PluginFieldKind.Radio or PluginFieldKind.SingleSelect => IsScalarValueType(type),
                PluginFieldKind.Checkbox or PluginFieldKind.MultipleSelect => IsEnumerableOfScalarValueType(type),
                PluginFieldKind.SingleEmployee => type == typeof(EmployeeRef),
                PluginFieldKind.MultipleEmployee => IsEnumerableOf(type, typeof(EmployeeRef)),
                PluginFieldKind.SingleDepartment => type == typeof(DepartmentRef),
                PluginFieldKind.MultipleDepartment => IsEnumerableOf(type, typeof(DepartmentRef)),
                PluginFieldKind.FileUpload or PluginFieldKind.ImageUpload => type == typeof(string) || IsEnumerableOf(type, typeof(string)),
                PluginFieldKind.TableForm => IsEnumerable(type),
                _ => true,
            };
        }

        private static bool IsScalarValueType(Type type)
        {
            return type == typeof(string)
                || type == typeof(bool)
                || IsNumericType(type);
        }

        private static bool IsEnumerableOfScalarValueType(Type type)
        {
            return IsEnumerable(type) && GetEnumerableItemTypes(type).Any(IsScalarValueType);
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(byte)
                || type == typeof(short)
                || type == typeof(int)
                || type == typeof(long)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal);
        }

        private static bool IsTimestampType(Type type)
        {
            return type == typeof(long)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset);
        }

        private static bool IsEnumerable(Type type)
        {
            return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static bool IsEnumerableOf(Type type, Type itemType)
        {
            if (type == typeof(string))
            {
                return false;
            }

            return GetEnumerableItemTypes(type).Any(x => x == itemType);
        }

        private static IEnumerable<Type> GetEnumerableItemTypes(Type type)
        {
            if (type == typeof(string))
            {
                yield break;
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType != null)
                {
                    yield return elementType;
                }
                yield break;
            }

            if (type.IsGenericType && type.GetGenericArguments().Length == 1)
            {
                yield return type.GetGenericArguments()[0];
            }

            foreach (var itemType in type.GetInterfaces()
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(x => x.GetGenericArguments()[0]))
            {
                yield return itemType;
            }
        }

        private static bool IsMultipleField(string fieldType, Type propertyType)
        {
            var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            return fieldType.ToLowerInvariant() switch
            {
                PluginFieldKind.Checkbox or PluginFieldKind.MultipleSelect => true,
                PluginFieldKind.MultipleEmployee or PluginFieldKind.MultipleDepartment => true,
                PluginFieldKind.TableForm => true,
                PluginFieldKind.FileUpload or PluginFieldKind.ImageUpload => type != typeof(string) && IsEnumerableOf(type, typeof(string)),
                _ => false,
            };
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
