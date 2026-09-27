using EIMSNext.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 授权组请求
    /// </summary>
    public class FormDataPermissionGroupRequest : RequestBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
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
        /// 成员列表
        /// </summary>
        public List<Member> Members { get; set; } = new List<Member>();
        /// <summary>
        /// 数据权限
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
