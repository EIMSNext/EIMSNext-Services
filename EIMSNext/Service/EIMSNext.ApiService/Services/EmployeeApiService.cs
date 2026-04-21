using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Common.Security;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class EmployeeApiService(IResolver resolver) : ApiServiceBase<Employee, EmployeeViewModel, IEmployeeService>(resolver)
    {
        protected override async Task AddAsyncCore(Employee entity)
        {
            var platform = GetCurrentCorpPlatform();
            if (platform == PlatformType.Private)
            {
                await BindPrivateUserAsync(entity, null, createWhenMissing: true);
            }
            else if (!entity.Approved && !string.IsNullOrEmpty(entity.Invite)
                && (!string.IsNullOrEmpty(entity.WorkPhone) || !string.IsNullOrEmpty(entity.WorkEmail)))
            {
                var user = CreateUser(entity, platform);
                Resolver.GetService<User>().Add(user);
                ApplyBoundUser(entity, user);
            }

            await base.AddAsyncCore(entity);
        }

        protected override async Task<ReplaceOneResult> ReplaceAsyncCore(Employee entity)
        {
            if (GetCurrentCorpPlatform() == PlatformType.Private)
            {
                var original = await CoreService.GetAsync(entity.Id) ?? throw new InvalidOperationException("员工不存在");
                await BindPrivateUserAsync(entity, original, createWhenMissing: false);
            }

            return await base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0)
            {
                return await base.DeleteAsyncCore(idList);
            }

            var empService = Resolver.GetService<Employee>();
            var userService = Resolver.GetService<User>();
            var employees = empService.Query(x => x.CorpId == IdentityContext.CurrentCorpId && idList.Contains(x.Id)).ToList();
            var isPrivate = GetCurrentCorpPlatform() == PlatformType.Private;

            foreach (var employee in employees)
            {
                employee.Status = 1;
                employee.DeleteFlag = true;

                if (isPrivate && !string.IsNullOrEmpty(employee.UserId))
                {
                    var user = userService.Get(employee.UserId);
                    if (user != null && !user.Disabled)
                    {
                        user.Disabled = true;
                        userService.Replace(user);
                    }
                }

                empService.Replace(employee);
            }

            return new { count = employees.Count };
        }

        private Task BindPrivateUserAsync(Employee entity, Employee? original, bool createWhenMissing)
        {
            var userService = Resolver.GetService<User>();
            var existingUserId = !string.IsNullOrWhiteSpace(entity.UserId) ? entity.UserId : original?.UserId;
            User? user = null;

            if (!string.IsNullOrWhiteSpace(existingUserId))
            {
                user = userService.Get(existingUserId);
                if (user == null || user.Disabled)
                {
                    throw new InvalidOperationException("关联用户不存在或已禁用");
                }
            }

            if (user == null)
            {
                if (!createWhenMissing)
                {
                    throw new InvalidOperationException("员工未绑定用户，无法同步更新");
                }

                EnsureUniqueContact(entity.WorkPhone, entity.WorkEmail, null);
                user = CreateUser(entity, PlatformType.Private);
                userService.Add(user);
            }
            else
            {
                EnsureUniqueContact(entity.WorkPhone, entity.WorkEmail, user.Id);
                user.Name = entity.EmpName;
                user.Phone = entity.WorkPhone;
                user.Email = entity.WorkEmail;
                userService.Replace(user);
            }

            ApplyBoundUser(entity, user);
            return Task.CompletedTask;
        }

        private void EnsureUniqueContact(string? phone, string? email, string? excludeUserId)
        {
            var userService = Resolver.GetService<User>();
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var duplicated = userService.Query(x => !x.Disabled && x.Phone == phone).FirstOrDefault();
                if (duplicated != null && duplicated.Id != excludeUserId)
                {
                    throw new InvalidOperationException("手机号已存在");
                }
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var duplicated = userService.Query(x => !x.Disabled && x.Email.ToLower() == email.ToLower()).FirstOrDefault();
                if (duplicated != null && duplicated.Id != excludeUserId)
                {
                    throw new InvalidOperationException("邮箱已存在");
                }
            }
        }

        private User CreateUser(Employee entity, PlatformType platform)
        {
            return new User
            {
                Phone = entity.WorkPhone,
                Email = entity.WorkEmail,
                Name = entity.EmpName,
                Platform = platform,
                Password = BCrypt.HashPassword("123456"),
                Crops = new List<UserCorp> { new() { CorpId = IdentityContext.CurrentCorpId, CorpType = "internal", IsDefault = true } }
            };
        }

        private static void ApplyBoundUser(Employee entity, User user)
        {
            entity.Approved = true;
            entity.UserId = user.Id;
            entity.UserName = user.Name;
        }

        private PlatformType GetCurrentCorpPlatform()
        {
            var corporate = Resolver.GetService<Corporate>().Get(IdentityContext.CurrentCorpId);
            return corporate?.Platform ?? PlatformType.Public;
        }
    }
}
