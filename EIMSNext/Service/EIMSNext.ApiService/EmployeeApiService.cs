using EIMSNext.ApiService.ViewModel;
using EIMSNext.Auth.Entity;
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using HKH.Common.Security;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class EmployeeApiService(IResolver resolver) : ApiServiceBase<Employee, EmployeeViewModel, IEmployeeService>(resolver)
    {
        protected override Task AddAsyncCore(Employee entity)
        {
            if (!entity.Approved && !string.IsNullOrEmpty(entity.Invite) 
                && (!string.IsNullOrEmpty(entity.WorkPhone) || !string.IsNullOrEmpty(entity.WorkEmail)))
            {
                var userService = Resolver.GetService<User>();
                var user = new User()
                {
                    Phone = entity.WorkPhone,
                    Email = entity.WorkEmail,
                    Name = entity.EmpName,
                    Platform = PlatformType.Public,
                    Password = BCrypt.HashPassword("123456"),
                    Crops = new List<UserCorp> { new UserCorp { CorpId = IdentityContext.CurrentCorpId, CorpType = "internal", IsDefault = true } }
                };

                userService.Add(user);
                entity.Approved = true;
                entity.UserId = user.Id;
                entity.UserName = user.Name;
            }

            return base.AddAsyncCore(entity);
        }
    }
}
