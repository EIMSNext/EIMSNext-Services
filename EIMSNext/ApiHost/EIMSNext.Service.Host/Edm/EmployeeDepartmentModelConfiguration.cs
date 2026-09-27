using EIMSNext.ApiService.ViewModels;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 注册 <see cref="EmployeeDepartmentViewModel"/> 为 OData 实体集
    /// <c>EmployeeDepartment</c>，供员工按部门过滤时导航。
    /// </summary>
    public class EmployeeDepartmentModelConfiguration : CorpModelConfigurationBase<EmployeeDepartmentViewModel>
    {
    }
}
