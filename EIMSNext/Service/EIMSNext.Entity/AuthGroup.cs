using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 表单发布/授权
    /// </summary>
    public class AuthGroup : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public string Desc { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public AuthGroupType Type { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<Member> Members
        { get; set; } = new List<Member>();
        /// <summary>
        /// 
        /// </summary>
        public DataPerms DataPerms { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? DataFilter { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum AuthGroupType
    {
        /// <summary>
        /// 
        /// </summary>
        ManageSelfData = 0,
        /// <summary>
        /// 
        /// </summary>
        ViewAllData = 1,
        /// <summary>
        /// 
        /// </summary>
        ManageAllData = 2,
        /// <summary>
        /// 
        /// </summary>
        Custom = 3,
    }

    /// <summary>
    /// 成员对象
    /// </summary>
    public class Member
    {
        /// <summary>
        /// 
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string? Code { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Label { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public MemberType Type { get; set; }
    }
    /// <summary>
    /// 成员类型，与前端的 SelectedTag组件的TagType对应
    /// </summary>
    public enum MemberType
    {
        /// <summary>
        /// 
        /// </summary>
        NotSet = 0,
        /// <summary>
        /// 
        /// </summary>
        Department,
        /// <summary>
        /// 
        /// </summary>
        Employee,
        /// <summary>
        /// 
        /// </summary>
        Role,
    }

    /// <summary>
    /// 数据权限
    /// </summary>
    [Flags]
    public enum DataPerms : long
    {
        /// <summary>
        /// 
        /// </summary>
        None = 0,
        /// <summary>
        /// 
        /// </summary>
        View = 1 << 0,
        /// <summary>
        /// 
        /// </summary>
        AddNew = 1 << 1,
        /// <summary>
        /// 
        /// </summary>
        Edit = 1 << 2,
        /// <summary>
        /// 
        /// </summary>
        Copy = 1 << 3,
        /// <summary>
        /// 
        /// </summary>
        Remove = 1 << 4,
        /// <summary>
        /// 
        /// </summary>
        Import = 1 << 5,
        /// <summary>
        /// 
        /// </summary>
        Export = 1 << 6,
    }

    /// <summary>
    /// 字段权限
    /// </summary>
    public class FieldPerm
    {
        /// <summary>
        /// 
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public bool Visible { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool Editable { get; set; }
    }
}
