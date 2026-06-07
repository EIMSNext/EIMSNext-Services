using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 授权组模板
    /// </summary>
    public class AuthGroupTemplate : EntityBase
    {
        /// <summary>
        /// 关联的应用模板ID
        /// </summary>
        public string AppTemplateId { get; set; } = string.Empty;
        /// <summary>
        /// 表单模板ID
        /// </summary>
        public string FormTemplateId { get; set; } = string.Empty;
        /// <summary>
        /// 授权组名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 授权组描述
        /// </summary>
        public string Desc { get; set; } = string.Empty;
        /// <summary>
        /// 授权组类型
        /// </summary>
        public AuthGroupType Type { get; set; }
        /// <summary>
        /// 数据权限（位标志）
        /// </summary>
        public long DataPerms { get; set; }
        /// <summary>
        /// 数据过滤条件
        /// </summary>
        public string? DataFilter { get; set; }
        /// <summary>
        /// 字段权限列表
        /// </summary>
        public List<FieldPerm> FieldPerms { get; set; } = new List<FieldPerm>();
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }
}
