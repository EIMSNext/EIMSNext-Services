using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 员工组视图模型。
    /// </summary>
    public class EmployeeGroupViewModel : EmployeeGroup
    {
        /// <summary>
        /// 获取或设置员工组分类。
        /// </summary>
        public EmployeeGroupCategory? EmployeeGroupCategory { get; set; }
    }
}

