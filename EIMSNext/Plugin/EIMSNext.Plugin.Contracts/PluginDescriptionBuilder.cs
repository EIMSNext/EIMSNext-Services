using System.Collections;
using System.Reflection;

namespace EIMSNext.Plugin.Contracts
{
    internal static class PluginDescriptionBuilder
    {
        private static readonly BindingFlags PluginMethodFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Type[] PluginSubListDefinitions =
        [
            typeof(PluginSubList<>),
            typeof(PluginSubList<,>),
            typeof(PluginSubList<,,>),
            typeof(PluginSubList<,,,>),
        ];

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
                string.Equals(method.GetCustomAttribute<PluginFunctionAttribute>()?.Id, functionId, StringComparison.Ordinal));
        }

        public static object? ProjectResult(MethodInfo method, object? value)
        {
            if (value == null)
            {
                return null;
            }

            var fields = BuildOutputValueMap(method).ToList();
            if (fields.Count == 0)
            {
                return value;
            }

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (property, key) in fields)
            {
                result[key] = property.GetValue(value);
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

            var parameterType = parameters[0].ParameterType;
            ValidatePluginFieldContract(method, parameterType, "input argument");
            foreach (var property in GetFieldProperties(parameterType))
            {
                var attribute = property.GetCustomAttribute<PluginInputAttribute>();
                var subListAttribute = property.GetCustomAttribute<PluginSubListAttribute>();
                if (attribute != null && subListAttribute != null)
                {
                    throw new InvalidOperationException(
                        $"Plugin function [{method.Name}] input [{property.Name}] cannot use PluginInput and PluginSubList together.");
                }

                if (attribute != null)
                {
                    yield return BuildInputField(method, property, attribute, allowCustomValue: attribute.AllowCustomValue);
                }
                else if (subListAttribute != null)
                {
                    yield return BuildInputSubListField(method, parameterType, property, subListAttribute);
                }
            }
        }

        private static IEnumerable<PluginResultFieldDesc> BuildOutputFields(MethodInfo method)
        {
            if (method.ReturnType == typeof(void))
            {
                yield break;
            }

            ValidatePluginFieldContract(method, method.ReturnType, "output result");
            foreach (var property in GetFieldProperties(method.ReturnType))
            {
                var attribute = property.GetCustomAttribute<PluginOutputAttribute>();
                var subListAttribute = property.GetCustomAttribute<PluginSubListAttribute>();
                if (attribute != null && subListAttribute != null)
                {
                    throw new InvalidOperationException(
                        $"Plugin function [{method.Name}] output [{property.Name}] cannot use PluginOutput and PluginSubList together.");
                }

                if (attribute != null)
                {
                    yield return BuildOutputField(method, property, attribute);
                }
                else if (subListAttribute != null)
                {
                    yield return BuildOutputSubListField(method, method.ReturnType, property, subListAttribute);
                }
            }
        }

        private static IEnumerable<(PropertyInfo Property, string Key)> BuildOutputValueMap(MethodInfo method)
        {
            if (method.ReturnType == typeof(void))
            {
                yield break;
            }

            foreach (var property in GetFieldProperties(method.ReturnType))
            {
                var attribute = property.GetCustomAttribute<PluginOutputAttribute>();
                var subListAttribute = property.GetCustomAttribute<PluginSubListAttribute>();
                if (attribute != null && subListAttribute != null)
                {
                    throw new InvalidOperationException(
                        $"Plugin function [{method.Name}] output [{property.Name}] cannot use PluginOutput and PluginSubList together.");
                }

                if (attribute != null)
                {
                    EnsureNotTableFormAttribute(method, property, attribute.FieldType, "output");
                    yield return (property, ResolveKey(property, attribute.Key));
                }
                else if (subListAttribute != null)
                {
                    ValidateSubListProperty(method, method.ReturnType, property);
                    yield return (property, ResolveKey(property, subListAttribute.Key));
                }
            }
        }

        private static PluginFieldDesc BuildInputField(MethodInfo method, PropertyInfo property, PluginInputAttribute attribute, bool allowCustomValue)
        {
            EnsureNotTableFormAttribute(method, property, attribute.FieldType, "input");
            ValidateInputType(method, property, attribute.FieldType);
            var field = new PluginFieldDesc
            {
                Key = ResolveKey(property, attribute.Key),
                Name = attribute.Name,
                FieldType = attribute.FieldType,
                Required = attribute.Required,
                AllowCustomValue = allowCustomValue,
                AllowFieldMapping = attribute.AllowFieldMapping,
                Multiple = IsMultipleField(attribute.FieldType, property.PropertyType),
                Description = attribute.Description,
            };

            foreach (var compatibleFieldType in attribute.CompatibleFieldTypes)
            {
                field.CompatibleFieldTypes.Add(compatibleFieldType);
            }

            return field;
        }

        private static PluginFieldDesc BuildInputSubListField(
            MethodInfo method,
            Type ownerType,
            PropertyInfo property,
            PluginSubListAttribute attribute)
        {
            var itemType = ValidateSubListProperty(method, ownerType, property);
            var field = new PluginFieldDesc
            {
                Key = ResolveKey(property, attribute.Key),
                Name = attribute.Name,
                FieldType = PluginFieldKind.TableForm,
                Required = attribute.Required,
                AllowCustomValue = false,
                AllowFieldMapping = false,
                Multiple = true,
                Description = attribute.Description,
            };

            foreach (var subProperty in GetFieldProperties(itemType))
            {
                var subAttribute = subProperty.GetCustomAttribute<PluginInputAttribute>();
                if (subAttribute == null)
                {
                    continue;
                }

                field.SubFields.Add(BuildInputField(method, subProperty, subAttribute, allowCustomValue: false));
            }

            EnsureSubFields(method, property, field.SubFields.Count);
            return field;
        }

        private static PluginResultFieldDesc BuildOutputField(MethodInfo method, PropertyInfo property, PluginOutputAttribute attribute)
        {
            EnsureNotTableFormAttribute(method, property, attribute.FieldType, "output");
            ValidateOutputType(method, property, attribute.FieldType);
            return new PluginResultFieldDesc
            {
                Key = ResolveKey(property, attribute.Key),
                Name = attribute.Name,
                FieldType = attribute.FieldType,
                Multiple = IsMultipleField(attribute.FieldType, property.PropertyType),
                Description = attribute.Description,
            };
        }

        private static PluginResultFieldDesc BuildOutputSubListField(
            MethodInfo method,
            Type ownerType,
            PropertyInfo property,
            PluginSubListAttribute attribute)
        {
            var itemType = ValidateSubListProperty(method, ownerType, property);
            var field = new PluginResultFieldDesc
            {
                Key = ResolveKey(property, attribute.Key),
                Name = attribute.Name,
                FieldType = PluginFieldKind.TableForm,
                Multiple = true,
                Description = attribute.Description,
            };

            foreach (var subProperty in GetFieldProperties(itemType))
            {
                var subAttribute = subProperty.GetCustomAttribute<PluginOutputAttribute>();
                if (subAttribute == null)
                {
                    continue;
                }

                field.SubFields.Add(BuildOutputField(method, subProperty, subAttribute));
            }

            EnsureSubFields(method, property, field.SubFields.Count);
            return field;
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

        private static void ValidatePluginFieldContract(MethodInfo method, Type type, string usage)
        {
            if (!typeof(IPluginField).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"Plugin function [{method.Name}] {usage} [{type.Name}] must implement IPluginField.");
            }
        }

        private static void EnsureNotTableFormAttribute(MethodInfo method, PropertyInfo property, string fieldType, string usage)
        {
            if (string.Equals(fieldType, PluginFieldKind.TableForm, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Plugin function [{method.Name}] {usage} [{property.Name}] must use PluginSubList instead of PluginFieldKind.TableForm.");
            }
        }

        private static Type ValidateSubListProperty(MethodInfo method, Type ownerType, PropertyInfo property)
        {
            var declaredItemTypes = GetDeclaredSubListItemTypes(ownerType).ToHashSet();
            if (declaredItemTypes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Plugin function [{method.Name}] field [{property.Name}] uses PluginSubList but owner [{ownerType.Name}] does not inherit PluginSubList<T>.");
            }

            var itemType = GetSingleEnumerableItemType(property.PropertyType);
            if (itemType == null)
            {
                throw new InvalidOperationException(
                    $"Plugin function [{method.Name}] field [{property.Name}] must be a generic list of a PluginSubList item type.");
            }

            ValidatePluginFieldContract(method, itemType, "sub list item");
            if (!declaredItemTypes.Contains(itemType))
            {
                throw new InvalidOperationException(
                    $"Plugin function [{method.Name}] field [{property.Name}] item [{itemType.Name}] must be declared by owner PluginSubList generic arguments.");
            }

            return itemType;
        }

        private static void EnsureSubFields(MethodInfo method, PropertyInfo property, int count)
        {
            if (count == 0)
            {
                throw new InvalidOperationException(
                    $"Plugin function [{method.Name}] sub list [{property.Name}] must declare at least one plugin field.");
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
                PluginFieldKind.TableForm => false,
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

        private static Type? GetSingleEnumerableItemType(Type type)
        {
            return GetEnumerableItemTypes(type).Distinct().SingleOrDefault();
        }

        private static IEnumerable<Type> GetDeclaredSubListItemTypes(Type type)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                if (!current.IsGenericType)
                {
                    continue;
                }

                var definition = current.GetGenericTypeDefinition();
                if (PluginSubListDefinitions.Contains(definition))
                {
                    foreach (var itemType in current.GetGenericArguments())
                    {
                        yield return itemType;
                    }
                }
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
