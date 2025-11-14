using EIMSNext.Auth.Entity;
using EIMSNext.Common.Extension;
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class CorporateService(IResolver resolver) : EntityServiceBase<Corporate>(resolver), ICorporateService
    {
        protected override async Task AddCoreAsync(IEnumerable<Corporate> entities, IClientSessionHandle? session)
        {
            var entity = entities.First();
            var deptRepo = Resolver.GetRepository<Department>();
            var empRepo = Resolver.GetRepository<Employee>();
            var clientRepo = Resolver.GetRepository<EIMSNext.Auth.Entity.Client>();
            var userRepo = Resolver.GetRepository<User>();
            var user = Context.User as User;

            entity.Platform = Context.User?.Platform ?? PlatformType.Public;
            if (string.IsNullOrEmpty(entity.Code))
                entity.Code = (Resolver.GetService<SerialNoSequence>() as ISerialNoSequenceService)!.NextCorpCode(entity.Platform);

            Repository.EnsureId(entity);

            var serviceClient = new Auth.Entity.Client
            {
                ClientName = "service_" + entity.Code,
                ClientSecrets = { },
                AllowedGrantTypes = IdentityServer4.Models.GrantTypes.ClientCredentials.Select(x => new ClientGrantType { GrantType = x }).ToList(),
                AllowedScopes = { new ClientScope { Scope = "api.readwrite" } },
                AccessTokenLifetime = 28800,
                IdentityTokenLifetime = 28800,
                CorpId = entity.Id
            };

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
                UserId = Context.Operator!.UserId!,
                UserName = Context.User?.Name ?? "",
                CorpId = entity.Id,
                Code = "E01",
                EmpName = Context.User?.Name ?? "",
                WorkEmail = Context.User?.Email ?? "",
                WorkPhone = Context.User?.Phone ?? "",
                DepartmentId = dept.Id,
                IsManager = false,
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

            user!.Crops.Add(new UserCorp { CorpId = entity.Id, CorpType = "internal", IsCorpOwner = true, IsDefault = true });

            var emp_system = new Employee { CorpId = entity.Id, Id = $"system_{entity.Id}", Code = "system", EmpName = "System", UserId = "system", UserName = "System", IsDummy = true };
            var emp_anonymous = new Employee { CorpId = entity.Id, Id = $"anonymous_{entity.Id}", Code = "anonymous", EmpName = "Anonymous", UserId = "anonymous", UserName = "Anonymous", IsDummy = true };

            var tasks = new List<Task>
            {
                base.AddCoreAsync(entities, session),
                clientRepo.InsertAsync(serviceClient,session),
                deptRepo.InsertAsync(dept, session),
                empRepo.InsertAsync(new List<Employee>{emp, emp_system,emp_anonymous}, session),
                userRepo.ReplaceAsync(user, session)
            };

            await Task.WhenAll(tasks);
        }
    }
}
