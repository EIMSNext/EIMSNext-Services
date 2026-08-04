using EIMSNext.Auth.Entities;
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
using EIMSNext.Service.Entities;
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

        public Task<UpdateResult> AddToRoleAsync(Role role, IEnumerable<string> empIds)
        {
            var update = UpdateBuilder.AddToSet(x => x.Roles, new EmpRole { RoleId = role.Id, RoleName = role.Name });
            var filter = FilterBuilder.And(FilterBuilder.In(x => x.Id, empIds),
                FilterBuilder.Not(FilterBuilder.ElemMatch(x => x.Roles, r => r.RoleId == role.Id) // 排除已存在该RoleId的员工
    )           );

            return Repository.UpdateManyAsync(filter, update, upsert: false);
        }

        public Task<UpdateResult> RemoveFromRoleAsync(string roleId, IEnumerable<string> empIds)
        {
            var update = UpdateBuilder.PullFilter(x => x.Roles, r => r.RoleId == roleId);
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
            var op = Context.Operator;
            var ip = Context.ClientIp;
            var currentCorpId = Context.CorpId;

            // 开一个事务作用域：主操作（Delete/Replace）与审计写入原子提交。
            // IRepository<T> 的无 session 重载通过 MongoTransactionScope.Transaction（AsyncLocal）自动拾取该事务。
            using var scope = NewTransactionScope();
            try
            {
                foreach (var employee in employees)
                {
                    var request = requestMap[employee.Id];
                    if (!approved)
                    {
                        await Repository.DeleteAsync(employee.Id);
                        await EmployeeDepartmentRepository.DeleteAsync(EmployeeDepartmentRepository.FilterBuilder.Eq(x => x.EmployeeId, employee.Id));
                        await RequestRepository.DeleteAsync(request.Id);

                        WriteAuditLog(
                            action: DbAction.Delete,
                            entityType: nameof(CorpOnboardingRequest),
                            dataId: request.Id,
                            detail: $"拒绝【{request.ApplicantName}】的加入企业申请",
                            now: reviewedTime, op: op, ip: ip, currentCorpId: currentCorpId);
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

                    await Repository.ReplaceAsync(employee);
                    await UserRepository.ReplaceAsync(user);
                    await RequestRepository.DeleteAsync(request.Id);

                    WriteAuditLog(
                        action: DbAction.Update,
                        entityType: nameof(CorpOnboardingRequest),
                        dataId: request.Id,
                        detail: $"审批通过【{request.ApplicantName}】的加入企业申请",
                        now: reviewedTime, op: op, ip: ip, currentCorpId: currentCorpId);
                }

                scope.CommitTransaction();
            }
            catch
            {
                // scope.Dispose() 在未 Commit 时自动 AbortTransaction
                throw;
            }
        }

        // 私有 helper：补齐系统字段，try/catch 防止审计失败阻断主操作
        // 调用方在 NewTransactionScope 内时，自动参与该事务
        private void WriteAuditLog(
            DbAction action,
            string entityType,
            string dataId,
            string detail,
            long now,
            Operator? op,
            string? ip,
            string currentCorpId)
        {
            try
            {
                AuditLogRepository.Insert(new AuditLog
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
                });
            }
            catch (Exception ex)
            {
                // Logger 是 ILogger<Employee>，直接调用
                Logger.LogError(ex,
                    "写入审计日志失败。EntityType={EntityType} DataId={DataId}",
                    entityType, dataId);
            }
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
