namespace EIMSNext.Common.Extension
{
    public static class DateTimeExtension
    {
        public static long ToTimeStampMs(this DateTime dt)
        {
            return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
        }

        public static DateTime ToDateTimeMs(this long ms)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }

        public static string DateFormat(this DateTime? dt)
        {
            if (dt == null) return string.Empty;
            return dt.Value.DateFormat();
        }
        public static string DateFormat(this DateTime dt)
        {
            return dt.ToString(Constants.Defaut_DateFormat);
        }
        public static string DateTimeFormat(this DateTime? dt)
        {
            if (dt == null) return string.Empty;
            return dt.Value.DateTimeFormat();
        }
        public static string DateTimeFormat(this DateTime dt)
        {
            return dt.ToString(Constants.Defaut_DateTimeFormat);
        }
    }
}
