using EIMSNext.Plugin.Contracts;
using EIMSNext.Service.Entities;

namespace EIMSNext.Component
{
    internal static class DataflowFieldMappingValidator
    {
        public static void ValidateFormFieldSettings(IEnumerable<FormFieldSetting> fieldSettings, string context)
        {
            foreach (var group in fieldSettings
                .Where(setting => setting.Field.IsSubField)
                .GroupBy(setting => GetSubTableName(setting.Field.Field))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key)))
            {
                var sourceKeys = group
                    .Select(setting => GetIterableSourceKey(setting.ValueField))
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (sourceKeys.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"{context} sub table field [{group.Key}] can only map single-result main fields or fields from the same iterable source.");
                }
            }
        }

        public static void ValidatePluginFieldSetting(PluginFieldSetting fieldSetting, bool isSubFieldSetting)
        {
            if (!string.Equals(fieldSetting.FieldType, PluginFieldKind.TableForm, StringComparison.OrdinalIgnoreCase))
            {
                if (!isSubFieldSetting
                    && fieldSetting.ValueType == PluginValueType.Field
                    && GetSubTableName(fieldSetting.ValueField?.Field) != null)
                {
                    throw new InvalidOperationException(
                        $"Plugin field [{fieldSetting.FieldKey}] cannot map a sub table field.");
                }

                return;
            }

            if (fieldSetting.SubFieldSettings.Count == 0)
            {
                return;
            }

            var subTableSources = fieldSetting.SubFieldSettings
                .Where(item => item.ValueType == PluginValueType.Field)
                .Select(item => GetIterableSourceKey(item.ValueField))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (subTableSources.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Plugin table field [{fieldSetting.FieldKey}] can only map single-result main fields or fields from the same iterable source.");
            }
        }

        private static string? GetIterableSourceKey(PluginFieldReference? field)
        {
            if (field == null)
            {
                return null;
            }

            return GetIterableSourceKey(field.NodeId, field.FormId, field.Field, field.SingleResultNode);
        }

        private static string? GetIterableSourceKey(FormFieldValueSetting? fieldSetting)
        {
            if (fieldSetting?.Field == null)
            {
                return null;
            }

            return GetIterableSourceKey(
                fieldSetting.Field.NodeId,
                fieldSetting.Field.FormId,
                fieldSetting.Field.Field,
                fieldSetting.SingleResultNode);
        }

        private static string? GetIterableSourceKey(string? nodeId, string? formId, string? field, bool? singleResultNode = true)
        {
            var subTableName = GetSubTableName(field);
            if (!string.IsNullOrWhiteSpace(subTableName))
            {
                return $"{nodeId ?? string.Empty}|{formId ?? string.Empty}|{subTableName}";
            }

            return singleResultNode == false
                ? $"{nodeId ?? string.Empty}|{formId ?? string.Empty}|master"
                : null;
        }

        private static string? GetSubTableName(string? field)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return null;
            }

            var parts = field.Split('>', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0])
                ? parts[0]
                : null;
        }
    }
}
