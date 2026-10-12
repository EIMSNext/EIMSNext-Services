namespace EIMSNext.Service
{
    /// <summary>
    /// 流水号取“当前日期”的统一入口：日期段与计数重置桶必须取自同一时区，否则两者会错位。
    /// TODO: 固定为服务器本地时区；将来应可配置为 UTC / LOCAL / 企业时区。
    /// </summary>
    internal static class SerialNoClock
    {
        public static DateTime Now => DateTime.Now;
    }
}
