using EIMSNext.ApiService.ViewModels;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Common.Security;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class EmployeeApiService(IResolver resolver) : ApiServiceBase<Employee, EmployeeViewModel, IEmployeeService>(resolver)
    {
        public Task ReviewJoinCorporateAsync(IEnumerable<string> employeeIds, bool approved)
        {
            return CoreService.ReviewJoinCorporateAsync(employeeIds, approved, IdentityContext.CurrentCorpId);
        }

        public Task AcceptInviteAsync(string userId, string? phone, string? email, bool accepted)
        {
            return CoreService.AcceptInviteAsync(userId, phone, email, accepted);
        }

        public async Task AddAsync(Employee entity, IEnumerable<EmployeeDepartmentRequest>? departments)
        {
            Resolver.GetRepository<Employee>().EnsureId(entity);
            var relations = BuildEmployeeDepartments(entity, departments);
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageEmployee(entity, null, relations.Select(x => x.DepartmentId));

            entity.Depts = BuildDepts(entity, departments);
            await AddAsync(entity);
            await ReplaceEmployeeDepartmentsAsync(entity.Id, relations);
        }

        public async Task<ReplaceOneResult> ReplaceAsync(Employee entity, IEnumerable<EmployeeDepartmentRequest>? departments, bool syncDepartments)
        {
            List<EmployeeDepartment>? relations = null;
            if (syncDepartments)
            {
                relations = BuildEmployeeDepartments(entity, departments);
                entity.Depts = BuildDepts(entity, departments);
            }

            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageEmployee(entity, original: null, relations?.Select(x => x.DepartmentId));
            var result = await ReplaceAsync(entity);

            if (syncDepartments)
            {
                await ReplaceEmployeeDepartmentsAsync(entity.Id, relations!);
            }

            return result;
        }

        public IQueryable<EmployeeViewModel> FilterByDepartment(IQueryable<EmployeeViewModel> query, string? departmentId, bool cascaded)
        {
            if (string.IsNullOrWhiteSpace(departmentId) || departmentId.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return query;
            }

            var departmentIds = GetDepartmentScopeIds(departmentId, cascaded);
            if (departmentIds.Count == 0)
            {
                return query.Where(x => false);
            }

            var relationRepo = Resolver.GetRepository<EmployeeDepartment>();
            var employeeIds = relationRepo.Queryable
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId
                    && !x.DeleteFlag
                    && departmentIds.Contains(x.DepartmentId))
                .Select(x => x.EmployeeId)
                .Distinct()
                .ToList();

            return query.Where(x => employeeIds.Contains(x.Id));
        }

        public List<string> GetAncestorDepartmentIds(IEnumerable<string> departmentIds)
        {
            var ids = departmentIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ids.Count == 0)
            {
                return [];
            }

            var departments = Resolver.GetRepository<Department>().Queryable
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag)
                .Select(x => new { x.Id, x.HeriarchyId })
                .ToList();
            var hierarchyIds = departments
                .Where(x => ids.Contains(x.Id))
                .Select(x => x.HeriarchyId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return departments
                .Where(x => hierarchyIds.Any(h => h.Contains($"|{x.Id}|")))
                .Select(x => x.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        protected override async Task AddAsyncCore(Employee entity)
        {
            var platform = GetCurrentCorpPlatform();
            if (platform == PlatformType.Private)
            {
                entity.Status = EmployeeStatus.Active;
                entity.UserBound = true;
                await BindPrivateUserAsync(entity, null, createWhenMissing: true);
            }
            else if (!string.IsNullOrWhiteSpace(entity.Invite)
                && (!string.IsNullOrWhiteSpace(entity.WorkPhone) || !string.IsNullOrWhiteSpace(entity.WorkEmail)))
            {
                entity.Status = EmployeeStatus.Active;
                entity.UserBound = false;
            }
            else
            {
                entity.Status = EmployeeStatus.Active;
                entity.UserBound = true;
            }

            await base.AddAsyncCore(entity);

            if (platform != PlatformType.Private && !string.IsNullOrWhiteSpace(entity.Invite)
                && (!string.IsNullOrWhiteSpace(entity.WorkPhone) || !string.IsNullOrWhiteSpace(entity.WorkEmail)))
            {
                await CreateAdminInviteRequestAsync(entity);
            }
        }

        protected override async Task<ReplaceOneResult> ReplaceAsyncCore(Employee entity)
        {
            var original = await CoreService.GetAsync(entity.Id) ?? throw new InvalidOperationException("员工不存在");
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageEmployee(entity, original);

            if (GetCurrentCorpPlatform() == PlatformType.Private)
            {
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

            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageEmployees(idList);

            foreach (var employee in employees)
            {
                employee.Status = EmployeeStatus.Inactive;
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

            var relationRepo = Resolver.GetRepository<EmployeeDepartment>();
            await relationRepo.DeleteAsync(relationRepo.FilterBuilder.In(x => x.EmployeeId, employees.Select(x => x.Id)));

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
            entity.UserBound = true;
            entity.Status = EmployeeStatus.Active;
            entity.UserId = user.Id;
            entity.UserName = user.Name;
        }

        private async Task CreateAdminInviteRequestAsync(Employee entity)
        {
            var requestService = Resolver.GetService<CorpOnboardingRequest>();
            var corporate = Resolver.GetService<Corporate>().Get(IdentityContext.CurrentCorpId);
            var exists = requestService.All().Any(x => x.EmployeeId == entity.Id);
            if (exists)
            {
                return;
            }

            var request = new CorpOnboardingRequest
            {
                UserId = string.Empty,
                UserName = string.Empty,
                TargetCorpId = IdentityContext.CurrentCorpId,
                TargetCorpName = corporate?.Name ?? string.Empty,
                ApplicantName = entity.EmpName,
                Phone = entity.WorkPhone,
                Email = entity.WorkEmail,
                EmployeeId = entity.Id,
                SourceType = CorpOnboardingSourceType.AdminInvite,
            };

            await requestService.AddAsync(request);
        }

        private PlatformType GetCurrentCorpPlatform()
        {
            var corporate = Resolver.GetService<Corporate>().Get(IdentityContext.CurrentCorpId);
            return corporate?.Platform ?? PlatformType.Public;
        }

        private List<EmployeeDepartment> BuildEmployeeDepartments(Employee entity, IEnumerable<EmployeeDepartmentRequest>? departments)
        {
            if (string.IsNullOrWhiteSpace(entity.CorpId))
            {
                entity.CorpId = IdentityContext.CurrentCorpId;
            }

            var items = departments?
                .Where(x => !string.IsNullOrWhiteSpace(x.DepartmentId))
                .Select((x, index) => new EmployeeDepartmentRequest
                {
                    DepartmentId = x.DepartmentId,
                    IsManager = x.IsManager,
                    SortValue = x.SortValue == 0 ? index : x.SortValue
                })
                .ToList() ?? [];

            if (items.Count == 0)
            {
                throw new BadRequestException("员工至少需要选择一个部门");
            }

            var duplicated = items
                .GroupBy(x => x.DepartmentId)
                .FirstOrDefault(x => x.Count() > 1);
            if (duplicated != null)
            {
                throw new BadRequestException("同一员工不能重复选择同一个部门");
            }

            var departmentIds = items.Select(x => x.DepartmentId).Distinct().ToList();
            var departmentRepo = Resolver.GetRepository<Department>();
            var validDepartments = departmentRepo.Queryable
                .Where(x => x.CorpId == entity.CorpId && !x.DeleteFlag && departmentIds.Contains(x.Id))
                .ToList();

            if (validDepartments.Count != departmentIds.Count)
            {
                throw new BadRequestException("员工部门不存在或不属于当前企业");
            }

            var relationRepo = Resolver.GetRepository<EmployeeDepartment>();
            return items.Select(x =>
            {
                var relation = new EmployeeDepartment
                {
                    CorpId = entity.CorpId,
                    EmployeeId = entity.Id,
                    DepartmentId = x.DepartmentId,
                    IsManager = x.IsManager,
                    SortValue = x.SortValue
                };
                relationRepo.EnsureId(relation);
                return relation;
            }).ToList();
        }

        private List<EmpDept> BuildDepts(Employee entity, IEnumerable<EmployeeDepartmentRequest>? departments)
        {
            if (string.IsNullOrWhiteSpace(entity.CorpId))
            {
                entity.CorpId = IdentityContext.CurrentCorpId;
            }

            var items = departments?
                .Where(x => !string.IsNullOrWhiteSpace(x.DepartmentId))
                .Select((x, index) => new EmployeeDepartmentRequest
                {
                    DepartmentId = x.DepartmentId,
                    IsManager = x.IsManager,
                    SortValue = x.SortValue == 0 ? index : x.SortValue
                })
                .ToList() ?? [];

            if (items.Count == 0)
            {
                return [];
            }

            var departmentIds = items.Select(x => x.DepartmentId).Distinct().ToList();
            var departmentRepo = Resolver.GetRepository<Department>();
            var departmentsMap = departmentRepo.Queryable
                .Where(x => x.CorpId == entity.CorpId && !x.DeleteFlag && departmentIds.Contains(x.Id))
                .ToDictionary(x => x.Id);

            return items
                .Where(x => departmentsMap.ContainsKey(x.DepartmentId))
                .OrderBy(x => x.SortValue)
                .Select(x =>
                {
                    var dept = departmentsMap[x.DepartmentId];
                    return new EmpDept
                    {
                        DeptId = dept.Id,
                        HeriarchyId = dept.HeriarchyId,
                        DeptName = dept.Name
                    };
                })
                .ToList();
        }

        private async Task ReplaceEmployeeDepartmentsAsync(string employeeId, IEnumerable<EmployeeDepartment> relations)
        {
            var relationRepo = Resolver.GetRepository<EmployeeDepartment>();
            await relationRepo.DeleteAsync(relationRepo.FilterBuilder.Eq(x => x.EmployeeId, employeeId));
            await relationRepo.InsertAsync(relations);
        }

        private List<string> GetDepartmentScopeIds(string departmentId, bool cascaded)
        {
            var departmentRepo = Resolver.GetRepository<Department>();
            if (!cascaded)
            {
                return departmentRepo.Queryable
                    .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.Id == departmentId)
                    .Select(x => x.Id)
                    .ToList();
            }

            return departmentRepo.Queryable
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId
                    && !x.DeleteFlag
                    && (x.Id == departmentId || x.HeriarchyId.Contains($"|{departmentId}|")))
                .Select(x => x.Id)
                .ToList();
        }
    }
}
