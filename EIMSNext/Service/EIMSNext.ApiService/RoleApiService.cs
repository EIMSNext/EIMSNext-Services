using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Core;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class RoleApiService(IResolver resolver) : ApiServiceBase<Role, RoleViewModel, IRoleService>(resolver)
    {
        public async Task AddEmployeesToRole(AddEmpsToRoleRequest request)
        {
            var role = CoreService.Get(request.RoleId);
            if (role != null)
            {
                var empService = Resolver.GetService<IEmployeeService, Employee>();
                await empService.AddToRoleAsync(role, request.EmpIds);
            }
        }

        public async Task RemoveEmployeesFromRole(RemoveEmpsToRoleRequest request)
        {
            var empService = Resolver.GetService<IEmployeeService, Employee>();
            await empService.RemoveFromRoleAsync(request.RoleId, request.EmpIds);
        }
    }
}
