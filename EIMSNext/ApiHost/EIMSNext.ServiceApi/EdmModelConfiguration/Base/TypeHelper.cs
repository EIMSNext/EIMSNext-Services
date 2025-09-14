using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;

namespace Microsoft.OData.ModelBuilder
{
    /// <summary>
    /// 
    /// </summary>
    public static class TypeHelper
    {
        /// <summary>
        /// Determine if a type is a generic type definition.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is a generic type definition; false otherwise.</returns>
        public static bool IsGenericTypeDefinition(this Type clrType)
        {
            return clrType.IsGenericTypeDefinition;
        }

        /// <summary>
        /// Determine if a type is an interface.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is an interface; false otherwise.</returns>
        public static bool IsInterface(Type clrType)
        {
            return clrType.IsInterface;
        }

        /// <summary>
        /// Determine if a type is a primitive.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is a primitive; false otherwise.</returns>
        public static bool IsPrimitive(Type clrType)
        {
            return clrType.IsPrimitive;
        }

        /// <summary>
        /// Determine if a type is assignable from another type.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <param name="fromType">The type to assign from.</param>
        /// <returns>True if the type is assignable; false otherwise.</returns>
        public static bool IsTypeAssignableFrom(Type clrType, Type fromType)
        {
            return clrType.IsAssignableFrom(fromType);
        }

        /// <summary>
        /// Determines whether the given type is IQueryable.
        /// </summary>
        /// <param name="type">The type</param>
        /// <returns><c>true</c> if the type is IQueryable.</returns>
        internal static bool IsIQueryable(Type type)
        {
            return type == typeof(IQueryable) ||
                (type != null && TypeHelper.IsGenericType(type) && type.GetGenericTypeDefinition() == typeof(IQueryable<>));
        }

        /// <summary>
        /// Determine if a type is a class.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is a class; false otherwise.</returns>
        public static bool IsClass(Type clrType)
        {
            return clrType.IsClass;
        }

        /// <summary>
        /// Determine if a type is a DateTime.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is a DateTime; false otherwise.</returns>
        public static bool IsDateTime(Type clrType)
        {
            Type underlyingTypeOrSelf = GetUnderlyingTypeOrSelf(clrType);
            return Type.GetTypeCode(underlyingTypeOrSelf) == TypeCode.DateTime;
        }

        /// <summary>
        /// Determine if a type is a DateOnly.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is a DateOnly; false otherwise.</returns>
        public static bool IsDateOnly(Type clrType)
        {
            Type underlyingTypeOrSelf = GetUnderlyingTypeOrSelf(clrType);
            return underlyingTypeOrSelf.FullName == "System.DateOnly";
        }

        /// <summary>
        /// Determine if a type is a TimeSpan.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is a TimeSpan; false otherwise.</returns>
        public static bool IsTimeSpan(Type clrType)
        {
            Type underlyingTypeOrSelf = GetUnderlyingTypeOrSelf(clrType);
            return underlyingTypeOrSelf == typeof(TimeSpan);
        }

        /// <summary>
        /// Determine if a type is an enumeration.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is an enumeration; false otherwise.</returns>
        public static bool IsEnum(Type clrType)
        {
            Type underlyingTypeOrSelf = GetUnderlyingTypeOrSelf(clrType);
            return underlyingTypeOrSelf.IsEnum;
        }

        public static Type GetUnderlyingTypeOrSelf(Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        /// <summary>
        /// Determine if a type is a collection.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is an enumeration; false otherwise.</returns>
        public static bool IsCollection(Type clrType)
        {
            return IsCollection(clrType, out Type _);
        }

        /// <summary>
        /// Determine if a type is a collection.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <param name="elementType">out: the element type of the collection.</param>
        /// <returns>True if the type is an enumeration; false otherwise.</returns>
        public static bool IsCollection(Type clrType, out Type elementType)
        {
            if (clrType == null)
            {
                throw new ArgumentNullException(nameof(clrType));
            }

            elementType = clrType;

            // see if this type should be ignored.
            if (clrType == typeof(string))
            {
                return false;
            }

            Type? collectionInterface
                = clrType.GetInterfaces()
                    .Union(new[] { clrType })
                    .FirstOrDefault(
                        t => t.IsGenericType
                             && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (collectionInterface != null)
            {
                elementType = collectionInterface.GetGenericArguments().Single();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determine if a type is visible.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is visible; false otherwise.</returns>
        public static bool IsVisible(Type clrType)
        {
            return clrType.IsVisible;
        }

        /// <summary>
        /// Return the base type from a type.
        /// </summary>
        /// <param name="clrType">The type to convert.</param>
        /// <returns>The base type from a type.</returns>
        public static Type? GetBaseType(Type clrType)
        {
            return clrType.BaseType;
        }

        /// <summary>
        /// Determine if a type is a value type.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is a value type; false otherwise.</returns>
        public static bool IsValueType(Type clrType)
        {
            return clrType.IsValueType;
        }

        /// <summary>
        /// Determine if a type is a generic type.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is a generic type; false otherwise.</returns>
        public static bool IsGenericType(this Type clrType)
        {
            return clrType.IsGenericType;
        }

        /// <summary>
        /// Return the type from a nullable type.
        /// </summary>
        /// <param name="clrType">The type to convert.</param>
        /// <returns>The type from a nullable type.</returns>
        public static Type ToNullable(Type clrType)
        {
            if (IsNullable(clrType))
            {
                return clrType;
            }
            else
            {
                return typeof(Nullable<>).MakeGenericType(clrType);
            }
        }

        /// <summary>
        /// Return the qualified name from a member info.
        /// </summary>
        /// <param name="memberInfo">The member info to convert.</param>
        /// <returns>The qualified name from a member info.</returns>
        public static string GetQualifiedName(MemberInfo memberInfo)
        {
            Contract.Assert(memberInfo != null);
            Type? type = memberInfo as Type;
            return type != null ? (type.Namespace + "." + type.Name) : memberInfo.Name;
        }

        /// <summary>
        /// Determine if a type is abstract.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is abstract; false otherwise.</returns>
        public static bool IsAbstract(Type clrType)
        {
            return clrType.IsAbstract;
        }

        /// <summary>
        /// Determine if a type is null-able.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is null-able; false otherwise.</returns>
        public static bool IsNullable(Type clrType)
        {
            if (TypeHelper.IsValueType(clrType))
            {
                // value types are only nullable if they are Nullable<T>
                return TypeHelper.IsGenericType(clrType) && clrType.GetGenericTypeDefinition() == typeof(Nullable<>);
            }
            else
            {
                // reference types are always nullable
                return true;
            }
        }

        /// <summary>
        /// Determine if a type is public.
        /// </summary>
        /// <param name="clrType">The type to test.</param>
        /// <returns>True if the type is public; false otherwise.</returns>
        public static bool IsPublic(Type clrType)
        {
            return clrType.IsPublic;
        }

        /// <summary>
        /// Return the reflected type from a member info.
        /// </summary>
        /// <param name="memberInfo">The member info to convert.</param>
        /// <returns>The reflected type from a member info.</returns>
        public static Type? GetReflectedType(MemberInfo memberInfo)
        {
            return memberInfo.ReflectedType;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Catching all exceptions in this case is the right to do.")]
        // This code is copied from DefaultHttpControllerTypeResolver.GetControllerTypes.
        internal static IEnumerable<Type?> GetLoadedTypes(IAssemblyResolver assembliesResolver)
        {
            if (assembliesResolver == null)
            {
                yield return null;
            }

            // Go through all assemblies referenced by the application and search for types matching a predicate
            IEnumerable<Assembly> assemblies = assembliesResolver!.Assemblies;
            foreach (Assembly assembly in assemblies)
            {
                Type?[]? exportedTypes = null;
                if (assembly == null || assembly.IsDynamic)
                {
                    // can't call GetTypes on a null (or dynamic?) assembly
                    continue;
                }

                try
                {
                    exportedTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    exportedTypes = ex.Types;
                }
                catch
                {
                    continue;
                }

                if (exportedTypes != null)
                {
                    foreach (var t in exportedTypes)
                    {
                        if ((t != null) && (TypeHelper.IsVisible(t)))
                        {
                            yield return t;
                        }
                    }
                }
            }
        }
    }
}