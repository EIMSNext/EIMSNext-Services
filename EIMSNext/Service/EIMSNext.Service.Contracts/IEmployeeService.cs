using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;

using MongoDB.Driver;

namespace EIMSNext.Service.Contracts
{
    public interface IEmployeeService : IService<Employee>
    {
        Task<UpdateResult> AddToRoleAsync(Role role, IEnumerable<string> empIds);
        Task<UpdateResult> RemoveFromRoleAsync(string roleId, IEnumerable<string> empIds);
    }
}
