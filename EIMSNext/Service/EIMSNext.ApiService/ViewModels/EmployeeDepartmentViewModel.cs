using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 员工与部门的归属关系视图模型。
    /// 作为 OData 实体集进入模型，使前端可对
    /// <c>Employee</c> 做 <c>Departments/any(...)</c> 过滤（替代 jsonb Depts 不可翻译的 Any）。
    /// </summary>
    public class EmployeeDepartmentViewModel : EmployeeDepartment
    {
    }
}
