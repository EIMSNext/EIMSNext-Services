namespace EIMSNext.Core.Entity
{
    /// <summary>
    /// 审计日志
    /// </summary>
    public class AuditLog : CorpEntityBase
    {
        /// <summary>
        /// 操作类型
        /// </summary>
        public DbAction Action { get; set; }
        /// <summary>
        /// 实体类型
        /// </summary>
        public string? EntityType { get; set; }
        /// <summary>
        /// 数据Id
        /// </summary>
        public string? DataId { get; set; }
        /// <summary>
        /// 日志详情，以 OldValue->NewValue生成可读性字符串
        /// </summary>
        public string? Detail { get; set; }
        /// <summary>
        /// 旧对象
        /// </summary>
        public string? OldData { get; set; }
        /// <summary>
        /// 新对象
        /// </summary>
        public string? NewData { get; set; }
        /// <summary>
        /// 批量更新/删除时的数据过滤条件
        /// </summary>
        public string? DataFilter { get; set; }
        /// <summary>
        /// 批量更新时的字段
        /// </summary>
        public string? UpdateExp { get; set; }
        /// <summary>
        /// 客户端IP
        /// </summary>
        public string? ClientIp {  get; set; }
    }
}
