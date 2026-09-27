using TSID.Creator.NET;

namespace EIMSNext.Common
{
    /// <summary>
    /// 基于 TSID（Time-Sorted Unique Identifier）的主键生成器。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 产出 13 位 Crockford base32 字符串（无连字符、URL 安全、大小写不敏感），
    /// 例如 <c>01226N0640J7Q</c>。相较原先的 32 位无连字符 GUID，TSID 具备以下特性：
    /// </para>
    /// <list type="bullet">
    /// <item><description>按生成时间近似有序，作为字符串列主键时索引局部性更好，减少 B 树页分裂；</description></item>
    /// <item><description>长度只有 13 位，主键索引与外键列占用更小；</description></item>
    /// <item><description>底层仍是 64 位整数，必要时可通过 <see cref="Tsid.ToLong"/> 转回数值。</description></item>
    /// </list>
    /// <para>生成器本身是线程安全的，可并发调用。</para>
    /// </remarks>
    public static class TsidIdGenerator
    {
        /// <summary>
        /// 生成一个新的 TSID 字符串。
        /// </summary>
        /// <returns>13 位 Crockford base32 字符串。</returns>
        public static string NewId() => TsidCreator.GetTsid().ToString();
    }
}
