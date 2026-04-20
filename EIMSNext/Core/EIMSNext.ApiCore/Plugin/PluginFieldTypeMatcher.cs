using EIMSNext.Common;

namespace EIMSNext.ApiCore.Plugin
{
    public static class PluginFieldTypeMatcher
    {
        public static bool IsCompatible(string sourceType, string targetType)
        {
            if (string.Equals(sourceType, targetType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsPair(sourceType, targetType, FieldType.Select1, FieldType.Radio)
                || IsPair(sourceType, targetType, FieldType.Select2, FieldType.CheckBox);
        }

        private static bool IsPair(string sourceType, string targetType, string left, string right)
        {
            return (string.Equals(sourceType, left, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(targetType, right, StringComparison.OrdinalIgnoreCase))
                || (string.Equals(sourceType, right, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(targetType, left, StringComparison.OrdinalIgnoreCase));
        }
    }
}
