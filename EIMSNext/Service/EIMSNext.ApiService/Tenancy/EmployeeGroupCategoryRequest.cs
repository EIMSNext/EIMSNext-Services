namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 员工组分类请求
    /// </summary>
    public class EmployeeGroupCategoryRequest : RequestBase
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
        /// 排序值
        /// </summary>
        public int SortValue { get; set; }
    }
}
