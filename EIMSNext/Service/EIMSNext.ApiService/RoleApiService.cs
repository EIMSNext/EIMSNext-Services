using System.Threading.Tasks;

using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Core;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class RoleApiService(IResolver resolver) : ApiServiceBase<Role, RoleViewModel>(resolver)
    {
        public async Task AddEmployeesToRole(AddEmpsToRoleRequest request)
        {
            var role = CoreService.Get(request.RoleId);
            if (role != null)
            {
                var empService = Resolver.GetService<Employee>() as IEmployeeService;
                await empService!.AddToRoleAsync(role, request.EmpIds);
            }
        }
    }
}
