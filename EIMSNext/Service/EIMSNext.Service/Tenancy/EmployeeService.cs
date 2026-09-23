using EIMSNext.Entities;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Service
{
    public class EmployeeService(IResolver resolver) : EntityServiceBase<Employee>(resolver), IEmployeeService
    {
        private IRepository<CorpOnboardingRequest> RequestRepository => Resolver.GetRepository<CorpOnboardingRequest>();
        private IRepository<User> UserRepository => Resolver.GetRepository<User>();
        private IRepository<EmployeeDepartment> EmployeeDepartmentRepository => Resolver.GetRepository<EmployeeDepartment>();

        public async Task<int> AddToEmployeeGroupAsync(EmployeeGroup employeeGroup, IEnumerable<string> empIds)
        {
            var idList = empIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
            if (idList.Count == 0)
            {
                return 0;
            }

            // 员工与员工组的归属由关系表 EmployeeGroupMember 承载：先排除已在组内的员工，再补齐缺失的关系行。
            var memberRepo = Resolver.GetRepository<EmployeeGroupMember>();
            var joinedEmployeeIds = memberRepo.Queryable
                .Where(x => idList.Contains(x.EmployeeId)
                    && x.EmployeeGroupId == employeeGroup.Id
                    && !x.DeleteFlag)
                .Select(x => x.EmployeeId)
                .ToList()
                .ToHashSet(StringComparer.Ordinal);

            var employeeIds = Repository.Queryable
                .Where(x => idList.Contains(x.Id) && !x.DeleteFlag)
                .Select(x => x.Id)
                .ToList();

            var relations = new List<EmployeeGroupMember>();
            foreach (var employeeId in employeeIds)
            {
                if (joinedEmployeeIds.Contains(employeeId))
                {
                    continue;
                }

                var member = new EmployeeGroupMember
                {
                    CorpId = employeeGroup.CorpId,
                    EmployeeId = employeeId,
                    EmployeeGroupId = employeeGroup.Id,
                    EmployeeGroupName = employeeGroup.Name
                };
                memberRepo.EnsureId(member);
                relations.Add(member);
            }

            if (relations.Count == 0)
            {
                return 0;
            }

            await memberRepo.InsertAsync(relations);
            return relations.Count;
        }

        public async Task<int> RemoveFromEmployeeGroupAsync(string employeeGroupId, IEnumerable<string> empIds)
        {
            var idList = empIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
            if (idList.Count == 0)
            {
                return 0;
            }

            // 关系表是唯一事实来源，直接删除匹配的归属行。
            var memberRepo = Resolver.GetRepository<EmployeeGroupMember>();
            var relations = memberRepo.Queryable
                .Where(x => idList.Contains(x.EmployeeId)
                    && x.EmployeeGroupId == employeeGroupId
                    && !x.DeleteFlag)
                .ToList();
            if (relations.Count == 0)
            {
                return 0;
            }

            await memberRepo.DeleteManyAsync(x => idList.Contains(x.EmployeeId) && x.EmployeeGroupId == employeeGroupId);
            return relations.Count;
        }

        public async Task ReviewJoinCorporateAsync(IEnumerable<string> employeeIds, bool approved, string corpId)
        {
            var idList = employeeIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
            if (idList.Count == 0)
            {
                throw new BadRequestException("申请不能为空");
            }

            var reviewedTime = DateTime.UtcNow.ToTimeStampMs();
            var op = Context.Operator;
            var ip = Context.ClientIp;
            var currentCorpId = Context.CorpId;

            List<AuditLog>? committedAuditLogs = null;
            await ExecuteWithTransactionRetryAsync(async () =>
            {
                var employees = Repository.Find(x => idList.Contains(x.Id) && !x.DeleteFlag).ToList();
                if (employees.Count != idList.Count)
                    throw new NotFoundException("部分员工不存在");
                if (employees.Any(x => x.CorpId != corpId || x.Status != EmployeeStatus.PendingReview))
                    throw new BadRequestException("包含无权审批或非待审核的员工");

                var requests = RequestRepository.Find(x => idList.Contains(x.EmployeeId) && x.TargetCorpId == corpId && x.SourceType == CorpOnboardingSourceType.UserApply).ToList();
                if (requests.Count != idList.Count)
                    throw new NotFoundException("部分加入申请不存在");
                var requestMap = requests.ToDictionary(x => x.EmployeeId, x => x);
                var userIds = requests.Select(x => x.UserId).Distinct().ToList();
                var users = UserRepository.Find(x => userIds.Contains(x.Id)).ToList().ToDictionary(x => x.Id, x => x);
                if (users.Count != userIds.Count)
                    throw new NotFoundException("申请用户不存在");

                var auditLogs = new List<AuditLog>();
                foreach (var employee in employees)
                {
                    var request = requestMap[employee.Id];
                    if (!approved)
                    {
                        await Repository.DeleteAsync(employee.Id);
                        await EmployeeDepartmentRepository.DeleteManyAsync(x => x.EmployeeId == employee.Id);
                        await RequestRepository.DeleteAsync(request.Id);

                        auditLogs.Add(CreateAuditLog(
                            action: DbAction.Delete,
                            entityType: nameof(CorpOnboardingRequest),
                            dataId: request.Id,
                            detail: $"拒绝【{request.ApplicantName}】的加入企业申请",
                            now: reviewedTime, op: op, ip: ip, currentCorpId: currentCorpId));
                        continue;
                    }

                    var user = users[request.UserId];
                    await AppendUserCorpAsync(user, employee.CorpId ?? string.Empty);
                    employee.Status = EmployeeStatus.Active;
                    employee.UserId = user.Id;
                    employee.UserName = user.Name;
                    employee.Invite = user.Id;
                    employee.UpdateBy = op;
                    employee.UpdateTime = reviewedTime;

                    await Repository.ReplaceAsync(employee);
                    await UserRepository.ReplaceAsync(user);
                    await RequestRepository.DeleteAsync(request.Id);

                    auditLogs.Add(CreateAuditLog(
                        action: DbAction.Update,
                        entityType: nameof(CorpOnboardingRequest),
                        dataId: request.Id,
                        detail: $"审批通过【{request.ApplicantName}】的加入企业申请",
                        now: reviewedTime, op: op, ip: ip, currentCorpId: currentCorpId));
                }
                committedAuditLogs = auditLogs;
            }).ConfigureAwait(false);

            if (committedAuditLogs is { Count: > 0 })
                await WriteAuditLogsAfterCommitAsync(committedAuditLogs).ConfigureAwait(false);
        }

        // 私有 helper：补齐系统字段，try/catch 防止审计失败阻断主操作
        // 调用方在 NewTransactionScope 内时，自动参与该事务
        private AuditLog CreateAuditLog(
            DbAction action,
            string entityType,
            string dataId,
            string detail,
            long now,
            Operator? op,
            string? ip,
            string currentCorpId)
        {
            return new AuditLog
            {
                    Action = action,
                    EntityType = entityType,
                    DataId = dataId,
                    Detail = detail,
                    CreateBy = op,
                    UpdateBy = op,
                    CreateTime = now,
                    UpdateTime = now,
                    ClientIp = ip,
                    CorpId = currentCorpId,
            };
        }

        private async Task WriteAuditLogsAfterCommitAsync(IReadOnlyCollection<AuditLog> logs)
        {
            async Task WriteAsync()
            {
                try { await AuditLogRepository.InsertAsync(logs).ConfigureAwait(false); }
                catch (Exception ex) { Logger.LogError(ex, "写入员工入职审计日志失败。Count={Count}", logs.Count); }
            }

            if (TransactionScope.IsInTransaction)
                await TransactionScope.RegisterAfterCommitAsync(DbContext, WriteAsync).ConfigureAwait(false);
            else
                await WriteAsync().ConfigureAwait(false);
        }

        public async Task AcceptInviteAsync(string userId, string? phone, string? email, bool accepted)
        {
            var normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            if (normalizedPhone is null && normalizedEmail is null)
            {
                throw new NotFoundException("未找到待处理的邀请");
            }

            // 这里用 lower() = lower() 表达同样的语义：ILike 会把值里的 % 与 _ 当成通配符，
            // 邮箱里带下划线时会误命中别的员工，因此不能用 ILike。
            var phoneKey = normalizedPhone?.ToLowerInvariant();
            var emailKey = normalizedEmail?.ToLowerInvariant();
            var invites = Repository.Queryable
                .Where(x => !x.DeleteFlag
                    && x.Status == EmployeeStatus.Active
                    && !x.UserBound
                    && ((phoneKey != null && x.WorkPhone.ToLower() == phoneKey)
                        || (emailKey != null && x.WorkEmail.ToLower() == emailKey)))
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
            await AppendUserCorpAsync(user, employee.CorpId ?? string.Empty);
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

        /// <summary>
        /// 把用户与企业的绑定写入关系表 UserCorp。
        /// 已绑定该企业时仅在缺少默认企业的情况下补设为默认；否则取消其它默认并新增。
        /// </summary>
        private async Task AppendUserCorpAsync(User user, string corpId)
        {
            var userCorpRepo = Resolver.GetRepository<UserCorp>();
            var userCorps = userCorpRepo.Queryable
                .Where(x => x.UserId == user.Id)
                .ToList();

            var current = userCorps.FirstOrDefault(x => x.CorpId == corpId);
            if (current != null)
            {
                if (!userCorps.Any(x => x.IsDefault))
                {
                    current.IsDefault = true;
                    await userCorpRepo.ReplaceAsync(current);
                }
                return;
            }

            foreach (var corp in userCorps.Where(x => x.IsDefault))
            {
                corp.IsDefault = false;
                await userCorpRepo.ReplaceAsync(corp);
            }

            var userCorp = new UserCorp
            {
                UserId = user.Id,
                CorpId = corpId,
                CorpType = "internal",
                IsCorpOwner = false,
                IsDefault = true
            };
            userCorpRepo.EnsureId(userCorp);
            await userCorpRepo.InsertAsync(userCorp);
        }
    }
}
