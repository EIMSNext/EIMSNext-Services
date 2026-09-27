namespace EIMSNext.Common.Extensions
{
    /// <summary>
    /// 提供 <see cref="DateTime"/> 类型的扩展方法。
    /// </summary>
    public static class DateTimeExtension
    {
        /// <summary>
        /// 将 <see cref="DateTime"/> 转换为 Unix 毫秒时间戳。
        /// </summary>
        /// <param name="dt">需要转换的日期时间。</param>
        /// <returns>对应的 Unix 毫秒时间戳。</returns>
        public static long ToTimeStampMs(this DateTime dt)
        {
            return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 将 Unix 毫秒时间戳转换为 <see cref="DateTime"/>（UTC 时间）。
        /// </summary>
        /// <param name="ms">Unix 毫秒时间戳。</param>
        /// <returns>对应的 UTC 日期时间。</returns>
        public static DateTime ToDateTimeMs(this long ms)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }

        /// <summary>
        /// 将可空的 <see cref="DateTime"/> 格式化为日期字符串。
        /// </summary>
        /// <param name="dt">需要格式化的日期时间，可为空。</param>
        /// <returns>格式化后的日期字符串；为空时返回空字符串。</returns>
        public static string DateFormat(this DateTime? dt)
        {
            if (dt == null) return string.Empty;
            return dt.Value.DateFormat();
        }

        /// <summary>
        /// 将 <see cref="DateTime"/> 格式化为日期字符串。
        /// </summary>
        /// <param name="dt">需要格式化的日期时间。</param>
        /// <returns>格式化后的日期字符串。</returns>
        public static string DateFormat(this DateTime dt)
        {
            return dt.ToString(Constants.Defaut_DateFormat);
        }

        /// <summary>
        /// 将可空的 <see cref="DateTime"/> 格式化为日期时间字符串。
        /// </summary>
        /// <param name="dt">需要格式化的日期时间，可为空。</param>
        /// <returns>格式化后的日期时间字符串；为空时返回空字符串。</returns>
        public static string DateTimeFormat(this DateTime? dt)
        {
            if (dt == null) return string.Empty;
            return dt.Value.DateTimeFormat();
        }

        /// <summary>
        /// 将 <see cref="DateTime"/> 格式化为日期时间字符串。
        /// </summary>
        /// <param name="dt">需要格式化的日期时间。</param>
        /// <returns>格式化后的日期时间字符串。</returns>
        public static string DateTimeFormat(this DateTime dt)
        {
            return dt.ToString(Constants.Defaut_DateTimeFormat);
        }
    }
}