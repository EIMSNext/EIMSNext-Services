using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 角色
    /// </summary>
    public class Role : CorpEntityBase
    {
        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 角色描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string RoleGroupId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public int SortValue { get; set; }
    }
}
