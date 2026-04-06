using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单发布/授权
    /// </summary>
    public class AuthGroup : CorpEntityBase
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
        public AuthGroupType Type { get; set; }
        /// <summary>
        /// 成员列表
        /// </summary>
        public List<Member> Members { get; set; } = new List<Member>();
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

    /// <summary>
    /// 授权组类型
    /// </summary>
    public enum AuthGroupType
    {
        /// <summary>
        /// 管理自身数据
        /// </summary>
        ManageSelfData,
        /// <summary>
        /// 查看所有数据
        /// </summary>
        ViewAllData,
        /// <summary>
        /// 管理所有数据
        /// </summary>
        ManageAllData,
        /// <summary>
        /// 自定义权限
        /// </summary>
        Custom,
    }

    /// <summary>
    /// 成员对象
    /// </summary>
    public class Member
    {
        /// <summary>
        /// 成员ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 成员编码
        /// </summary>
        public string? Code { get; set; }
        /// <summary>
        /// 成员显示名称
        /// </summary>
        public string Label { get; set; } = string.Empty;
        /// <summary>
        /// 成员类型
        /// </summary>
        public MemberType Type { get; set; }
        /// <summary>
        /// 是否级联部门
        /// </summary>
        public bool CascadedDept { get; set; }
    }
    /// <summary>
    /// 成员类型，与前端的 SelectedTag组件的TagType对应
    /// </summary>
    public enum MemberType
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// 部门
        /// </summary>
        Department,
        /// <summary>
        /// 员工
        /// </summary>
        Employee,
        /// <summary>
        /// 角色
        /// </summary>
        Role
    }

    /// <summary>
    /// 数据权限
    /// </summary>
    [Flags]
    public enum DataPerms : long
    {
        /// <summary>
        /// 无权限
        /// </summary>
        None = 0,
        /// <summary>
        /// 查看
        /// </summary>
        View = 1 << 0,
        /// <summary>
        /// 新增
        /// </summary>
        AddNew = 1 << 1,
        /// <summary>
        /// 编辑
        /// </summary>
        Edit = 1 << 2,
        /// <summary>
        /// 复制
        /// </summary>
        Copy = 1 << 3,
        /// <summary>
        /// 删除
        /// </summary>
        Remove = 1 << 4,
        /// <summary>
        /// 导入
        /// </summary>
        Import = 1 << 5,
        /// <summary>
        /// 导出
        /// </summary>
        Export = 1 << 6,
        /// <summary>
        /// 所有权限
        /// </summary>
        All = (1 << 7) - 1,
    }

    /// <summary>
    /// 字段权限
    /// </summary>
    public class FieldPerm
    {
        /// <summary>
        /// 字段ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 是否可见
        /// </summary>
        public bool Visible { get; set; }
        /// <summary>
        /// 是否可编辑
        /// </summary>
        public bool Editable { get; set; }
        /// <summary>
        /// 是否允许表格中插入行
        /// </summary>
        public bool? TableInsert { get; set; }
        /// <summary>
        /// 是否允许表格中编辑行
        /// </summary>
        public bool? TableEdit { get; set; }
        /// <summary>
        /// 是否允许表格中删除行
        /// </summary>
        public bool? TableDelete { get; set; }
    }
}
