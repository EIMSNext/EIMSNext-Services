using EIMSNext.Core.Abstractions;

namespace EIMSNext.Core.Entities
{
    /// <summary>
    /// 审计日志
    /// </summary>
    public class AuditLog : LogEntityBase
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
        /// 字段变更列表，JSON 数组 [{field,type,label,old,new}]；可读串由前端/导出侧格式化。
        /// 批量 ExecuteUpdate 只记录被设置的字段（old 恒 null），条件另存 DataFilter。
        /// </summary>
        public string? Detail { get; set; }
        /// <summary>
        /// 批量更新/删除时的条件字符串，与 Detail 分列存放。
        /// </summary>
        public string? DataFilter { get; set; }
        /// <summary>
        /// 客户端IP
        /// </summary>
        public string? ClientIp { get; set; }
    }
}
