using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 员工与员工组的归属关系视图模型。
    /// 作为 OData 实体集进入模型，使前端可对
    /// <c>Employee</c> 做 <c>Groups/any(...)</c> 过滤。
    /// </summary>
    public class EmployeeGroupMemberViewModel : EmployeeGroupMember
    {
    }
}
