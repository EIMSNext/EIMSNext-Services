using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 角色组
    /// </summary>
    public class EmployeeGroupCategory : CorpEntityBase
    {
        /// <summary>
        /// 角色组名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 角色组描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 角色组排序值
        /// </summary>
        public int SortValue { get; set; }
    }
}
