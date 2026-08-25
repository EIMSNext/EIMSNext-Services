using EIMSNext.Auth.Entities;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

using HKH.Mef2.Integration;

using MongoDB.Driver;

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
            CreateGate.Wait();
            try
            {
                base.Add(entities);
            }
            finally
            {
                CreateGate.Release();
            }
        }

        protected override async Task AddCoreAsync(IEnumerable<Corporate> entities, IClientSessionHandle? session)
        {
            var entity = entities.First();
            var deptRepo = Resolver.GetRepository<Department>();
            var empRepo = Resolver.GetRepository<Employee>();
            var empDeptRepo = Resolver.GetRepository<EmployeeDepartment>();
            var adminGroupRepo = Resolver.GetRepository<AdminGroup>();
            var userRepo = Resolver.GetRepository<User>();
            var user = Context.User as User;

            entity.Platform = Context.User?.Platform ?? PlatformType.Public;
            if (string.IsNullOrEmpty(entity.Code))
                entity.Code = (Resolver.GetService<SerialNoSequence>() as ISerialNoSequenceService)!.NextCorpCode(entity.Platform);

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

            emp.Depts = new List<EmpDept>
            {
                new() { DeptId = dept.Id, HeriarchyId = dept.HeriarchyId, DeptName = dept.Name }
            };

            dept.CreateBy = Context.Operator;
            dept.CreateTime = DateTime.UtcNow.ToTimeStampMs();
            dept.UpdateBy = dept.CreateBy;
            dept.UpdateTime = DateTime.UtcNow.ToTimeStampMs();

            emp.CreateBy = Context.Operator;
            emp.CreateTime = DateTime.UtcNow.ToTimeStampMs();
            emp.UpdateBy = emp.CreateBy;
            emp.UpdateTime = DateTime.UtcNow.ToTimeStampMs();

            if (!user!.Crops.Any(x => x.CorpId == entity.Id))
            {
                foreach (var corp in user.Crops)
                {
                    corp.IsDefault = false;
                }

                user.Crops.Add(new UserCorp { CorpId = entity.Id, CorpType = "internal", IsCorpOwner = true, IsDefault = true });
            }

            var empDepartments = new List<EmployeeDepartment>
            {
                new() { CorpId = entity.Id, EmployeeId = emp.Id, DepartmentId = dept.Id, SortValue = 0 },
            };
            empDeptRepo.EnsureId(empDepartments);
            var systemAdminGroup = new AdminGroup
            {
                CorpId = entity.Id,
                Name = "系统管理员",
                Type = AdminGroupType.System,
                ParentId = string.Empty,
                SortValue = -1,
                EmployeeIds = []
            };
            adminGroupRepo.EnsureId(systemAdminGroup);

            await base.AddCoreAsync(entities, session);
            await deptRepo.InsertAsync(dept, session);
            await empRepo.InsertAsync(new List<Employee> { emp }, session);
            await empDeptRepo.InsertAsync(empDepartments, session);
            await adminGroupRepo.InsertAsync(systemAdminGroup, session);
            await userRepo.ReplaceAsync(user, session);
        }
    }
}
