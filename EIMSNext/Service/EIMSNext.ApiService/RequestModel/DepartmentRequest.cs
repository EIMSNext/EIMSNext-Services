namespace EIMSNext.ApiService.RequestModel
{
    /// <summary>
    /// 
    /// </summary>
    public class DepartmentRequest : RequestBase
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
    }
}
