using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 授权组模板
    /// </summary>
    public class FormDataPermissionGroupTemplate : EntityBase
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
        public FormDataPermissionMode Type { get; set; }
        /// <summary>
        /// 数据权限（位标志）
        /// </summary>
        public long FormDataPermissions { get; set; }
        /// <summary>
        /// 数据过滤条件
        /// </summary>
        public string? DataFilter { get; set; }
        /// <summary>
        /// 字段权限列表
        /// </summary>
        public List<FormFieldPermission> FormFieldPermissions { get; set; } = new List<FormFieldPermission>();
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }
}
