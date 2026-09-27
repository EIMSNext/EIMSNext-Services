using EIMSNext.Core.Services;
using EIMSNext.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface IEmployeeService : IService<Employee>
    {
        Task<int> AddToEmployeeGroupAsync(EmployeeGroup role, IEnumerable<string> empIds);
        Task<int> RemoveFromEmployeeGroupAsync(string employeeGroupId, IEnumerable<string> empIds);
        Task ReviewJoinCorporateAsync(IEnumerable<string> employeeIds, bool approved, string corpId);
        Task AcceptInviteAsync(string userId, string? phone, string? email, bool accepted);
    }
}
