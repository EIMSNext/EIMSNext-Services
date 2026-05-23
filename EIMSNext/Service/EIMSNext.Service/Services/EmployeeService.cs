using EIMSNext.Auth.Entities;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class EmployeeService(IResolver resolver) : EntityServiceBase<Employee>(resolver), IEmployeeService
    {
        private IRepository<CorpOnboardingRequest> RequestRepository => Resolver.GetRepository<CorpOnboardingRequest>();
        private IRepository<User> UserRepository => Resolver.GetRepository<User>();

        public Task<UpdateResult> AddToRoleAsync(Role role, IEnumerable<string> empIds)
        {
            var update = UpdateBuilder.AddToSet(x => x.Roles, new EmpRole { RoleId = role.Id, RoleName = role.Name });
            var filter = FilterBuilder.And(FilterBuilder.In(x => x.Id, empIds),
                FilterBuilder.Not(FilterBuilder.ElemMatch(x => x.Roles, r => r.RoleId == role.Id) // 排除已存在该RoleId的员工
    )           );

            return Repository.UpdateManyAsync(filter, update);
        }

        public Task<UpdateResult> RemoveFromRoleAsync(string roleId, IEnumerable<string> empIds)
        {
            var update = UpdateBuilder.PullFilter(x => x.Roles, r => r.RoleId == roleId);
            var filter = FilterBuilder.In(x => x.Id, empIds);

            return Repository.UpdateManyAsync(filter, update);
        }

        public async Task ReviewJoinCorporateAsync(IEnumerable<string> employeeIds, bool approved, string corpId)
        {
            var idList = employeeIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
            if (idList.Count == 0)
            {
                throw new BadRequestException("申请不能为空");
            }

            var employees = Repository.Queryable
                .Where(x => idList.Contains(x.Id) && !x.DeleteFlag)
                .ToList();
            if (employees.Count != idList.Count)
            {
                throw new NotFoundException("部分员工不存在");
            }

            if (employees.Any(x => x.CorpId != corpId || x.Status != EmployeeStatus.PendingReview))
            {
                throw new BadRequestException("包含无权审批或非待审核的员工");
            }

            var requests = RequestRepository.Queryable
                .Where(x => idList.Contains(x.EmployeeId) && x.TargetCorpId == corpId && x.SourceType == CorpOnboardingSourceType.UserApply)
                .ToList();
            if (requests.Count != idList.Count)
            {
                throw new NotFoundException("部分加入申请不存在");
            }

            var requestMap = requests.ToDictionary(x => x.EmployeeId, x => x);
            var userIds = requests.Select(x => x.UserId).Distinct().ToList();
            var users = UserRepository.Queryable.Where(x => userIds.Contains(x.Id)).ToList().ToDictionary(x => x.Id, x => x);
            if (users.Count != userIds.Count)
            {
                throw new NotFoundException("申请用户不存在");
            }

            var reviewedTime = DateTime.UtcNow.ToTimeStampMs();
            foreach (var employee in employees)
            {
                var request = requestMap[employee.Id];
                if (!approved)
                {
                    await Repository.DeleteAsync(employee.Id);
                    await RequestRepository.DeleteAsync(request.Id);
                    await AuditLogRepository.InsertAsync(new AuditLog
                    {
                        Action = DbAction.Delete,
                        EntityType = nameof(CorpOnboardingRequest),
                        DataId = request.Id,
                        Detail = $"拒绝【{request.ApplicantName}】的加入企业申请"
                    });
                    continue;
                }

                var user = users[request.UserId];
                AppendUserCorp(user, employee.CorpId ?? string.Empty);
                employee.Status = EmployeeStatus.Active;
                employee.UserId = user.Id;
                employee.UserName = user.Name;
                employee.Invite = user.Id;
                employee.UpdateBy = Context.Operator;
                employee.UpdateTime = reviewedTime;

                await Repository.ReplaceAsync(employee);
                await UserRepository.ReplaceAsync(user);
                await RequestRepository.DeleteAsync(request.Id);
                await AuditLogRepository.InsertAsync(new AuditLog
                {
                    Action = DbAction.Update,
                    EntityType = nameof(CorpOnboardingRequest),
                    DataId = request.Id,
                    Detail = $"审批通过【{request.ApplicantName}】的加入企业申请"
                });
            }
        }

        public async Task AcceptInviteAsync(string userId, string? phone, string? email, bool accepted)
        {
            var invites = Repository.Queryable
                .Where(x => !x.DeleteFlag && x.Status == EmployeeStatus.Active && !x.UserBound)
                .ToList()
                .Where(x =>
                    (!string.IsNullOrWhiteSpace(phone) && string.Equals(x.WorkPhone, phone, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(email) && string.Equals(x.WorkEmail, email, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (invites.Count == 0)
            {
                throw new NotFoundException("未找到待处理的邀请");
            }

            var employee = invites[0];
            var request = RequestRepository.Queryable.FirstOrDefault(x => x.EmployeeId == employee.Id && x.SourceType == CorpOnboardingSourceType.AdminInvite);
            if (request == null)
            {
                throw new NotFoundException("邀请记录不存在");
            }

            if (!accepted)
            {
                employee.Status = EmployeeStatus.Inactive;
                employee.UpdateBy = Context.Operator;
                employee.UpdateTime = DateTime.UtcNow.ToTimeStampMs();
                await Repository.ReplaceAsync(employee);
                await RequestRepository.DeleteAsync(request.Id);
                return;
            }

            var user = UserRepository.Get(userId) ?? throw new NotFoundException("用户不存在");
            AppendUserCorp(user, employee.CorpId ?? string.Empty);
            employee.UserId = user.Id;
            employee.UserName = user.Name;
            employee.UserBound = true;
            employee.Invite = user.Id;
            employee.UpdateBy = Context.Operator;
            employee.UpdateTime = DateTime.UtcNow.ToTimeStampMs();

            await Repository.ReplaceAsync(employee);
            await UserRepository.ReplaceAsync(user);
            await RequestRepository.DeleteAsync(request.Id);
        }

        private static void AppendUserCorp(User user, string corpId)
        {
            if (user.Crops.Any(x => x.CorpId == corpId))
            {
                if (!user.Crops.Any(x => x.IsDefault))
                {
                    var current = user.Crops.First(x => x.CorpId == corpId);
                    current.IsDefault = true;
                }
                return;
            }

            if (user.Crops.Any())
            {
                foreach (var corp in user.Crops)
                {
                    corp.IsDefault = false;
                }
            }

            user.Crops.Add(new UserCorp
            {
                CorpId = corpId,
                CorpType = "internal",
                IsCorpOwner = false,
                IsDefault = true
            });
        }
    }
}
