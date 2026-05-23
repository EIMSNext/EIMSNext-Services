using EIMSNext.Auth.Entities;
using EIMSNext.Common.Extensions;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class CorpOnboardingService(IResolver resolver)
        : EntityServiceBase<CorpOnboardingRequest>(resolver), ICorpOnboardingService
    {
        protected override bool LogicDelete => false;

        private IRepository<Corporate> CorporateRepository => Resolver.GetRepository<Corporate>();
        private IRepository<Department> DepartmentRepository => Resolver.GetRepository<Department>();
        private IRepository<Employee> EmployeeRepository => Resolver.GetRepository<Employee>();

        public async Task ApplyJoinCorporateAsync(string corpId, User user)
        {
            if (string.IsNullOrWhiteSpace(corpId))
            {
                throw new BadRequestException("请选择要加入的企业");
            }

            if (user.Crops.Any(x => x.CorpId == corpId))
            {
                throw new ConflictException("当前用户已加入该企业");
            }

            var corporate = CorporateRepository.Get(corpId);
            if (corporate == null || corporate.DeleteFlag)
            {
                throw new NotFoundException("企业不存在");
            }

            var rootDepartment = DepartmentRepository.Queryable
                .Where(x => !x.DeleteFlag && x.CorpId == corpId)
                .OrderBy(x => x.Code)
                .FirstOrDefault();
            if (rootDepartment == null)
            {
                throw new BadRequestException("目标企业缺少默认部门");
            }

            var hasPending = Repository.Queryable.Any(x =>
                x.UserId == user.Id &&
                x.TargetCorpId == corpId &&
                x.SourceType == CorpOnboardingSourceType.UserApply);
            if (hasPending)
            {
                throw new ConflictException("已提交加入申请，请勿重复提交");
            }

            var now = DateTime.UtcNow.ToTimeStampMs();
            var employee = new Employee
            {
                CorpId = corporate.Id,
                Code = GeneratePendingEmployeeCode(corporate.Id),
                EmpName = user.Name,
                WorkPhone = user.Phone ?? string.Empty,
                WorkEmail = user.Email ?? string.Empty,
                DepartmentId = rootDepartment.Id,
                IsManager = false,
                UserBound = true,
                Invite = user.Id,
                UserId = user.Id,
                UserName = user.Name,
                Status = EmployeeStatus.PendingReview,
                CreateBy = Context.Operator,
                UpdateBy = Context.Operator,
                CreateTime = now,
                UpdateTime = now,
            };
            EmployeeRepository.EnsureId(employee);

            var onboardingRequest = new CorpOnboardingRequest
            {
                UserId = user.Id,
                UserName = user.Name,
                TargetCorpId = corporate.Id,
                TargetCorpName = corporate.Name,
                ApplicantName = employee.EmpName,
                Phone = employee.WorkPhone,
                Email = employee.WorkEmail,
                EmployeeId = employee.Id,
                SourceType = CorpOnboardingSourceType.UserApply,
            };
            Repository.EnsureId(onboardingRequest);

            await EmployeeRepository.InsertAsync(employee);
            await AddCoreAsync([onboardingRequest], null);
        }

        private string GeneratePendingEmployeeCode(string corpId)
        {
            var count = EmployeeRepository.Queryable.Count(x => x.CorpId == corpId && !x.DeleteFlag) + 1;
            return $"P{count:D3}";
        }
    }
}
