using EIMSNext.Entities;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace EIMSNext.Service
{
    public class EmployeeService(IResolver resolver) : EntityServiceBase<Employee>(resolver), IEmployeeService
    {
        private IRepository<CorpOnboardingRequest> RequestRepository => Resolver.GetRepository<CorpOnboardingRequest>();
        private IRepository<User> UserRepository => Resolver.GetRepository<User>();
        private IRepository<EmployeeDepartment> EmployeeDepartmentRepository => Resolver.GetRepository<EmployeeDepartment>();

        public Task<UpdateResult> AddToEmployeeGroupAsync(EmployeeGroup employeeGroup, IEnumerable<string> empIds)
        {
            var update = UpdateBuilder.AddToSet(x => x.EmployeeGroups, new EmployeeGroupRef { EmployeeGroupId = employeeGroup.Id, EmployeeGroupName = employeeGroup.Name });
            var filter = FilterBuilder.And(FilterBuilder.In(x => x.Id, empIds),
                FilterBuilder.Not(FilterBuilder.ElemMatch(x => x.EmployeeGroups, r => r.EmployeeGroupId == employeeGroup.Id) // 排除已存在该EmployeeGroupId的员工
    )           );

            return Repository.UpdateManyAsync(filter, update, upsert: false);
        }

        public Task<UpdateResult> RemoveFromEmployeeGroupAsync(string employeeGroupId, IEnumerable<string> empIds)
        {
            var update = UpdateBuilder.PullFilter(x => x.EmployeeGroups, r => r.EmployeeGroupId == employeeGroupId);
            var filter = FilterBuilder.In(x => x.Id, empIds);

            return Repository.UpdateManyAsync(filter, update, upsert: false);
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
            await ExecuteWithTransactionRetryAsync(async session =>
            {
                var employees = Repository.Find(x => idList.Contains(x.Id) && !x.DeleteFlag, session).ToList();
                if (employees.Count != idList.Count)
                    throw new NotFoundException("部分员工不存在");
                if (employees.Any(x => x.CorpId != corpId || x.Status != EmployeeStatus.PendingReview))
                    throw new BadRequestException("包含无权审批或非待审核的员工");

                var requests = RequestRepository.Find(x => idList.Contains(x.EmployeeId) && x.TargetCorpId == corpId && x.SourceType == CorpOnboardingSourceType.UserApply, session).ToList();
                if (requests.Count != idList.Count)
                    throw new NotFoundException("部分加入申请不存在");
                var requestMap = requests.ToDictionary(x => x.EmployeeId, x => x);
                var userIds = requests.Select(x => x.UserId).Distinct().ToList();
                var users = UserRepository.Find(x => userIds.Contains(x.Id), session).ToList().ToDictionary(x => x.Id, x => x);
                if (users.Count != userIds.Count)
                    throw new NotFoundException("申请用户不存在");

                var auditLogs = new List<AuditLog>();
                foreach (var employee in employees)
                {
                    var request = requestMap[employee.Id];
                    if (!approved)
                    {
                        await Repository.DeleteAsync(employee.Id, session);
                        await EmployeeDepartmentRepository.DeleteAsync(EmployeeDepartmentRepository.FilterBuilder.Eq(x => x.EmployeeId, employee.Id), session);
                        await RequestRepository.DeleteAsync(request.Id, session);

                        auditLogs.Add(CreateAuditLog(
                            action: DbAction.Delete,
                            entityType: nameof(CorpOnboardingRequest),
                            dataId: request.Id,
                            detail: $"拒绝【{request.ApplicantName}】的加入企业申请",
                            now: reviewedTime, op: op, ip: ip, currentCorpId: currentCorpId));
                        continue;
                    }

                    var user = users[request.UserId];
                    AppendUserCorp(user, employee.CorpId ?? string.Empty);
                    employee.Status = EmployeeStatus.Active;
                    employee.UserId = user.Id;
                    employee.UserName = user.Name;
                    employee.Invite = user.Id;
                    employee.UpdateBy = op;
                    employee.UpdateTime = reviewedTime;

                    await Repository.ReplaceAsync(employee, session);
                    await UserRepository.ReplaceAsync(user, session);
                    await RequestRepository.DeleteAsync(request.Id, session);

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

            if (MongoTransactionScope.IsInTransaction)
                await MongoTransactionScope.RegisterAfterCommitAsync(WriteAsync).ConfigureAwait(false);
            else
                await WriteAsync().ConfigureAwait(false);
        }

        public async Task AcceptInviteAsync(string userId, string? phone, string? email, bool accepted)
        {
            var identityFilters = new List<FilterDefinition<Employee>>();
            if (!string.IsNullOrWhiteSpace(phone))
            {
                identityFilters.Add(Repository.FilterBuilder.Regex(
                    x => x.WorkPhone,
                    new BsonRegularExpression($"^{Regex.Escape(phone.Trim())}$", "i")));
            }
            if (!string.IsNullOrWhiteSpace(email))
            {
                identityFilters.Add(Repository.FilterBuilder.Regex(
                    x => x.WorkEmail,
                    new BsonRegularExpression($"^{Regex.Escape(email.Trim())}$", "i")));
            }
            if (identityFilters.Count == 0)
            {
                throw new NotFoundException("未找到待处理的邀请");
            }

            var inviteFilter = Repository.FilterBuilder.And(
                Repository.FilterBuilder.Eq(x => x.DeleteFlag, false),
                Repository.FilterBuilder.Eq(x => x.Status, EmployeeStatus.Active),
                Repository.FilterBuilder.Eq(x => x.UserBound, false),
                Repository.FilterBuilder.Or(identityFilters));
            var invites = Repository.Collection.Find(inviteFilter).ToList();
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
