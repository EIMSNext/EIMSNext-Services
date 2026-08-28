using EIMSNext.Core.Services;
using EIMSNext.Entities;

using MongoDB.Driver;

namespace EIMSNext.Service.Contracts
{
    public interface IEmployeeService : IService<Employee>
    {
        Task<UpdateResult> AddToEmployeeGroupAsync(EmployeeGroup role, IEnumerable<string> empIds);
        Task<UpdateResult> RemoveFromEmployeeGroupAsync(string employeeGroupId, IEnumerable<string> empIds);
        Task ReviewJoinCorporateAsync(IEnumerable<string> employeeIds, bool approved, string corpId);
        Task AcceptInviteAsync(string userId, string? phone, string? email, bool accepted);
    }
}
