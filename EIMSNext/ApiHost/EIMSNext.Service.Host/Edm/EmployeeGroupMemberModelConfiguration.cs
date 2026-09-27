using EIMSNext.ApiService.ViewModels;

namespace EIMSNext.Service.Host.Edm
{
    /// <summary>
    /// 注册 <see cref="EmployeeGroupMemberViewModel"/> 为 OData 实体集
    /// <c>EmployeeGroupMember</c>，供员工按员工组过滤时导航。
    /// </summary>
    public class EmployeeGroupMemberModelConfiguration : CorpModelConfigurationBase<EmployeeGroupMemberViewModel>
    {
    }
}
