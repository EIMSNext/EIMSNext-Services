using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Service.Entities
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
        /// 角色所属角色组 ID
        /// </summary>
        public string RoleGroupId { get; set; } = "";
        /// <summary>
        /// 角色排序值
        /// </summary>
        public int SortValue { get; set; }
    }
}
