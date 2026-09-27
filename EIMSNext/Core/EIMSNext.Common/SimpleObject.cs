namespace EIMSNext.Common
{
    /// <summary>
    /// 表示仅包含 Id、Code、Name 三个基本字段的简单对象。
    /// </summary>
    public class SimpleObject
    {
        /// <summary>
        /// 获取或设置对象标识。
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置对象编码。
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置对象名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}