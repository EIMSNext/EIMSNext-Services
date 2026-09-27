using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 员工组
    /// </summary>
    public class EmployeeGroup : CorpEntityBase
    {
        /// <summary>
        /// 员工组名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 员工组描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 所属员工组分类 ID
        /// </summary>
        public string EmployeeGroupCategoryId { get; set; } = "";
        /// <summary>
        /// 员工组排序值
        /// </summary>
        public int SortValue { get; set; }
    }
}
