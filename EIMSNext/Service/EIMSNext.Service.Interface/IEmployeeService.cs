using EIMSNext.Core.Service;
using EIMSNext.Entity;

using MongoDB.Driver;

namespace EIMSNext.Service.Interface
{
    public interface IEmployeeService : IService<Employee>
    {
        Task<UpdateResult> AddToRoleAsync(Role role, IEnumerable<string> empIds);
        Task<UpdateResult> RemoveFromRoleAsync(string roleId, IEnumerable<string> empIds);
    }
}
