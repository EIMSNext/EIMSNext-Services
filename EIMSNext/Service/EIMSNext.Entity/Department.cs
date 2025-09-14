using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 部门
    /// </summary>
    public class Department : CorpEntityBase
    {
        /// <summary>
        /// 部门编码
        /// </summary>
        public string Code { get; set; } = "";
        /// <summary>
        /// 部门名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 是否公司
        /// </summary>
        public bool IsCompany { get; set; }
        /// <summary>
        /// 父级部门Id
        /// </summary>
        public string ParentId { get; set; } = "";
        /// <summary>
        /// 父级部门名称
        /// </summary>
        public string ParentName { get; set; } = "";
        /// <summary>
        /// 所有父级部门ID串，以|分隔（前后均有|），子在前父在后
        /// </summary>
        public string HeriarchyId { get; set; } = "";
        /// <summary>
        /// 所有父级部门名称，以/分隔，子在前父在后
        /// </summary>
        public string HeriarchyName { get; set; } = "";
    }
}
