using EIMSNext.Entities;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;

using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Service
{
    public class CorporateService(IResolver resolver) : EntityServiceBase<Corporate>(resolver), ICorporateService
    {
        private static readonly SemaphoreSlim CreateGate = new(1, 1);

        public override Task AddAsync(Corporate entity) => AddWithGateAsync([entity]);

        public override Task AddAsync(IEnumerable<Corporate> entities) => AddWithGateAsync(entities);

        public override void Add(Corporate entity) => AddWithGate([entity]);

        public override void Add(IEnumerable<Corporate> entities) => AddWithGate(entities);

        private async Task AddWithGateAsync(IEnumerable<Corporate> entities)
        {
            await CreateGate.WaitAsync();
            try
            {
                await base.AddAsync(entities);
            }
            finally
            {
                CreateGate.Release();
            }
        }

        private void AddWithGate(IEnumerable<Corporate> entities)
        {
            AddWithGateAsync(entities).GetAwaiter().GetResult();
        }

        protected override async Task AddCoreAsync(IEnumerable<Corporate> entities)
        {
            var entity = entities.First();
            var deptRepo = Resolver.GetRepository<Department>();
            var empRepo = Resolver.GetRepository<Employee>();
            var empDeptRepo = Resolver.GetRepository<EmployeeDepartment>();
            var adminGroupRepo = Resolver.GetRepository<TenantAdminGroup>();
            var userRepo = Resolver.GetRepository<User>();
            var user = Context.User as User;

            entity.Platform = Context.User?.Platform ?? PlatformType.Public;

            Repository.EnsureId(entity);

            var dept = new Department
            {
                CorpId = entity.Id,
                Code = "0",
                Name = entity.Name
            };

            deptRepo.EnsureId(dept);
            dept.HeriarchyId = $"|{dept.Id}|";
            dept.HeriarchyName = dept.Name;

            var emp = new Employee
            {
                UserId = Context.UserId,
                UserName = Context.User?.Name ?? "",
                CorpId = entity.Id,
                Code = "E01",
                EmpName = Context.User?.Name ?? "",
                WorkEmail = Context.User?.Email ?? "",
                WorkPhone = Context.User?.Phone ?? "",
            };
            empRepo.EnsureId(emp);

            dept.CreateBy = Context.Operator;
            dept.CreateTime = DateTime.UtcNow.ToTimeStampMs();
            dept.UpdateBy = dept.CreateBy;
            dept.UpdateTime = DateTime.UtcNow.ToTimeStampMs();

            emp.CreateBy = Context.Operator;
            emp.CreateTime = DateTime.UtcNow.ToTimeStampMs();
            emp.UpdateBy = emp.CreateBy;
            emp.UpdateTime = DateTime.UtcNow.ToTimeStampMs();

            // 用户与企业的绑定由关系表 UserCorp 承载，
            // 绑定时必须显式写入 UserId。
            var userCorpRepo = Resolver.GetRepository<UserCorp>();
            var userCorps = userCorpRepo.Queryable
                .Where(x => x.UserId == user!.Id)
                .ToList();
            if (userCorps.All(x => x.CorpId != entity.Id))
            {
                foreach (var corp in userCorps.Where(x => x.IsDefault))
                {
                    corp.IsDefault = false;
                    await userCorpRepo.ReplaceAsync(corp);
                }

                var userCorp = new UserCorp
                {
                    UserId = user!.Id,
                    CorpId = entity.Id,
                    CorpType = "internal",
                    IsCorpOwner = true,
                    IsDefault = true
                };
                userCorpRepo.EnsureId(userCorp);
                await userCorpRepo.InsertAsync(userCorp);
            }

            var empDepartments = new List<EmployeeDepartment>
            {
                new() { CorpId = entity.Id, EmployeeId = emp.Id, DepartmentId = dept.Id, HeriarchyId = dept.HeriarchyId, SortValue = 0 },
            };
            empDeptRepo.EnsureId(empDepartments);
            var systemTenantAdminGroup = new TenantAdminGroup
            {
                CorpId = entity.Id,
                Name = "系统管理员",
                Type = TenantAdminGroupType.System,
                ParentId = string.Empty,
                SortValue = -1,
                EmployeeIds = []
            };
            adminGroupRepo.EnsureId(systemTenantAdminGroup);

            await base.AddCoreAsync(entities);
            await deptRepo.InsertAsync(dept);
            await empRepo.InsertAsync(new List<Employee> { emp });
            await empDeptRepo.InsertAsync(empDepartments);
            await adminGroupRepo.InsertAsync(systemTenantAdminGroup);
            await userRepo.ReplaceAsync(user);
        }
    }
}
