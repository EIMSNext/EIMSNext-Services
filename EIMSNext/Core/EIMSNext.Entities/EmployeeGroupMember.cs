using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 员工与员工组的归属关系（独立关系表 "EmployeeGroupMember"）。
    /// 原来该关系内嵌在 <c>Employee.EmployeeGroups</c> 的 jsonb 数组里；
    /// 迁移到 PostgreSQL 后提升为独立关系表，是该关系的唯一事实来源
    /// （jsonb 投影已删除，读写一律走本表）。
    /// </summary>
    public class EmployeeGroupMember : CorpEntityBase
    {
        /// <summary>员工 ID。</summary>
        public string EmployeeId { get; set; } = "";

        /// <summary>员工组 ID。</summary>
        public string EmployeeGroupId { get; set; } = "";

        /// <summary>
        /// 员工组名称快照。关系表只存 ID，但列表展示需要名称，
        /// 保留快照可避免每次读员工都回表 join 员工组。
        /// </summary>
        public string EmployeeGroupName { get; set; } = "";

        /// <summary>在同一员工组内的显示排序（值越小越靠前）。</summary>
        public int SortValue { get; set; }
    }
}
