using System.Diagnostics.Contracts;
using System.Globalization;

using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Formatter.Deserialization;
using Microsoft.AspNetCore.OData.Formatter.Serialization;
using Microsoft.OData;
using Microsoft.OData.Edm;

namespace EIMSNext.Service.Host.OData
{
    /// <summary>
    /// 
    /// </summary>
    public class LowercaseODataEnumSerializer : ODataEnumSerializer
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="serializerProvider"></param>
        public LowercaseODataEnumSerializer(IODataSerializerProvider serializerProvider) : base(serializerProvider)
        {
        }

        /// <summary>
        /// Creates an <see cref="ODataEnumValue"/> for the object represented by <paramref name="graph"/>.
        /// </summary>
        /// <param name="graph">The enum value.</param>
        /// <param name="enumType">The EDM enum type of the value.</param>
        /// <param name="writeContext">The serializer write context.</param>
        /// <returns>The created <see cref="ODataEnumValue"/>.</returns>
        public override ODataEnumValue CreateODataEnumValue(object graph, IEdmEnumTypeReference enumType,
            ODataSerializerContext writeContext)
        {
            if (graph != null)
            {
                ODataMetadataLevel metadataLevel = writeContext != null ? writeContext.MetadataLevel : ODataMetadataLevel.Minimal;
                var enumValue = new ODataEnumValue(Convert.ToInt64(graph).ToString(), enumType.FullName());
                AddTypeNameAnnotationAsNeeded(enumValue, enumType, metadataLevel);
                return enumValue;
            }

            return base.CreateODataEnumValue(graph, enumType, writeContext);
        }

        //从父类源码中复制
        internal static void AddTypeNameAnnotationAsNeeded(ODataEnumValue enumValue, IEdmEnumTypeReference enumType, ODataMetadataLevel metadataLevel)
        {
            Contract.Assert(enumValue != null);
            if (ShouldAddTypeNameAnnotation(metadataLevel))
            {
                string? typeName;
                if (ShouldSuppressTypeNameSerialization(metadataLevel))
                {
                    typeName = null;
                }
                else
                {
                    typeName = enumType.FullName();
                }

                enumValue.TypeAnnotation = new ODataTypeAnnotation(typeName);
            }
        }

        private static bool ShouldAddTypeNameAnnotation(ODataMetadataLevel metadataLevel)
        {
            switch (metadataLevel)
            {
                case ODataMetadataLevel.Minimal:
                    return false;
                case ODataMetadataLevel.Full:
                case ODataMetadataLevel.None:
                default:
                    return true;
            }
        }

        private static bool ShouldSuppressTypeNameSerialization(ODataMetadataLevel metadataLevel)
        {
            Contract.Assert(metadataLevel != ODataMetadataLevel.Minimal);

            switch (metadataLevel)
            {
                case ODataMetadataLevel.None:
                    return true;
                case ODataMetadataLevel.Full:
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class LowercaseODataEnumDeserializer : ODataEnumDeserializer
    {
        public override Task<object> ReadAsync(ODataMessageReader messageReader, Type type, ODataDeserializerContext readContext)
        {
            return base.ReadAsync(messageReader, type, readContext);
        }
        /// <inheritdoc />
        public override object ReadInline(object item, IEdmTypeReference edmType, ODataDeserializerContext readContext)
        {
            if (item != null && readContext != null)
            {
                var enumValue = item as ODataEnumValue;
                if (enumValue != null)
                {
                    // The client serializer emits enum values as numeric strings (for example, "1").
                    // OData's name parser does not reliably convert that representation, so handle it
                    // explicitly before falling back to named enum members.
                    if (long.TryParse(enumValue.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
                    {
                        return ConvertEnumValue(edmType, numericValue);
                    }

                    if (edmType.AsEnum().EnumDefinition().TryParseEnum(enumValue.Value, true, out long result))
                    {
                        return ConvertEnumValue(edmType, result);
                    }
                }
            }

            return base.ReadInline(item, edmType, readContext);
        }

        private static object ConvertEnumValue(IEdmTypeReference edmType, long value)
        {
            var fullName = edmType.AsEnum().EnumDefinition().FullName();
            var clrType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, throwOnError: false))
                .FirstOrDefault(type => type?.IsEnum == true);

            return clrType == null
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                : Enum.ToObject(clrType, value);
        }
    }
}
