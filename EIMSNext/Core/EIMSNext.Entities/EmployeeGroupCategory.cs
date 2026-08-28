using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 员工组分类
    /// </summary>
    public class EmployeeGroupCategory : CorpEntityBase
    {
        /// <summary>
        /// 员工组分类名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 员工组分类描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 员工组分类排序值
        /// </summary>
        public int SortValue { get; set; }
    }
}
