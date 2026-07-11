using EIMSNext.ApiCore;
using EIMSNext.ApiService;
using EIMSNext.Auth.Entities;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Http;

namespace EIMSNext.ApiHost.Authorization
{
    /// <summary>
    /// 身份标识上下文
    /// </summary>
    public class IdentityContext : IIdentityContext
    {
        private bool _retrieved = false;

        private IdentityType _type = IdentityType.None;
        private PublicScope _publicScope = PublicScope.None;
        private IResolver _resolver;
        private User? _user;
        private Employee? _employee;
        private string _systemObjectName = string.Empty;

        /// <summary>
        ///
        /// </summary>
        /// <param name="resolver"></param>
        public IdentityContext(IResolver resolver)
        {
            _resolver = resolver;
            IHttpContextAccessor httpContextAccessor = resolver.Resolve<IHttpContextAccessor>();
            AccessToken = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault() ?? "";
            var idClaim = httpContextAccessor.HttpContext?.User.FindFirst(AuthClaimTypes.Id);
            var corpClaim = httpContextAccessor.HttpContext?.User.FindFirst("corp");
            var identityTypeClaim = httpContextAccessor.HttpContext?.User.FindFirst(AuthClaimTypes.IdentityType);
            var nameClaim = httpContextAccessor.HttpContext?.User.FindFirst(AuthClaimTypes.Name);
            var dashboardIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(AuthClaimTypes.DashboardId);
            var publicTargetIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(AuthClaimTypes.PublicTargetId);
            var publicScopeClaim = httpContextAccessor.HttpContext?.User.FindFirst(AuthClaimTypes.PublicScope);
            CurrentUserID = idClaim?.Value ?? string.Empty;
            CurrentCorpId = corpClaim?.Value ?? string.Empty;
            CurrentDashboardId = publicTargetIdClaim?.Value ?? dashboardIdClaim?.Value ?? string.Empty;
            _publicScope = ParsePublicScope(publicScopeClaim?.Value);
            _systemObjectName = nameClaim?.Value ?? string.Empty;

            if (string.Equals(identityTypeClaim?.Value, "public", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(CurrentDashboardId))
            {
                _type = IdentityType.Public;
                _user = new User { Id = "public", Name = "public" };
                _employee = new Employee
                {
                    Id = "public",
                    CorpId = CurrentCorpId,
                    UserId = "public",
                    UserName = "public",
                    Code = "public",
                    EmpName = "public",
                    IsDummy = true,
                    UserBound = true,
                    Status = EmployeeStatus.Active,
                };
                _retrieved = true;
            }

            if (string.Equals(identityTypeClaim?.Value, IdentityType.System.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _type = IdentityType.System;
                CurrentUserID = "system";
                _retrieved = true;
            }

            if (idClaim == null && corpClaim == null)
            {
                var client_idClaim = httpContextAccessor.HttpContext?.User.FindFirst("client_id");
                var clientId = client_idClaim?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(clientId))
                {
                    var client = resolver.GetService<EIMSNext.Auth.Entities.Client>().Get(clientId);
                    if (client != null)
                    {
                        CurrentCorpId = client.CorpId;
                        CurrentUserID = "system";
                        _type = IdentityType.Client;
                        _retrieved = true;
                    }
                }
            }

            var serviceContext = resolver.GetServiceContext();
            serviceContext.AccessToken = AccessToken;
            serviceContext.CorpId = CurrentCorpId;
            serviceContext.User = CurrentUser;
            serviceContext.Employee = CurrentEmployee;
            serviceContext.Operator = _type == IdentityType.System
                ? new Operator("system", _systemObjectName, "System")
                : CurrentEmployee?.ToOperator() ?? Operator.Empty;
            serviceContext.ClientIp = IpHelper.GetClientIp(httpContextAccessor);
        }

        private static PublicScope ParsePublicScope(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return PublicScope.None;
            return Enum.TryParse<PublicScope>(value.Trim(), ignoreCase: true, out var parsed)
                && parsed is not PublicScope.None
                && IsSingleScope(parsed)
                ? parsed
                : PublicScope.None;
        }

        private static bool IsSingleScope(PublicScope scope)
        {
            var value = (int)scope;
            return value > 0 && (value & (value - 1)) == 0;
        }

        /// <summary>
        /// 当前用户ID
        /// </summary>
        public string CurrentUserID { get; private set; } = string.Empty;

        /// <summary>
        /// 当前用户对象
        /// </summary>
        public IUser? CurrentUser
        {
            get
            {
                if (_user == null && !_retrieved)
                    Retrieve();
                return _user;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public IEmployee? CurrentEmployee
        {
            get
            {
                if (_employee == null && !_retrieved)
                    Retrieve();
                return _employee;
            }
        }

        private void Retrieve()
        {
            if (!_retrieved)
            {
                _user = _resolver.GetService<User>().Get(CurrentUserID);
                if (_user != null)
                {
                    if (string.IsNullOrWhiteSpace(CurrentCorpId))
                    {
                        CurrentCorpId = _user.Crops.FirstOrDefault(x => x.IsDefault)?.CorpId
                            ?? _user.Crops.FirstOrDefault()?.CorpId
                            ?? string.Empty;
                    }

                    _employee = _resolver.GetService<Employee>().Query(x => x.CorpId == CurrentCorpId && x.UserId == _user.Id).FirstOrDefault();
                }
                _retrieved = true;
            }
        }

        /// <summary>
        /// 用户身份
        /// </summary>
        public IdentityType IdentityType
        {
            get
            {
                if (_type == IdentityType.None && CurrentUser != null && CurrentUser is User)
                {
                    var corp = ((User)CurrentUser).Crops.FirstOrDefault(x => x.CorpId == CurrentCorpId);
                    if (corp != null)
                    {
                        if (corp.IsCorpOwner)
                        {
                            _type = IdentityType.CorpOwmer;
                        }
                        else
                        {
                            if (_user!.Disabled)
                            {
                                _type = IdentityType.Disabled;
                            }
                            else if (_employee != null)
                            {
                                var adminGroups = _resolver.GetService<AdminGroup>().All()
                                    .Where(x =>
                                        x.CorpId == CurrentCorpId &&
                                        !x.DeleteFlag &&
                                        x.EmployeeIds.Contains(_employee.Id));

                                if (adminGroups.Any(x => x.Type == AdminGroupType.System))
                                {
                                    _type = IdentityType.CorpAdmin;
                                }
                                else if (adminGroups.Any(x => x.Type == AdminGroupType.Normal))
                                {
                                    _type = IdentityType.AppAdmin;
                                }
                                else
                                {
                                    _type = IdentityType.Employee;
                                }
                            }
                            else
                            {
                                _type = IdentityType.Employee;
                            }

                        }
                    }
                    else
                    {
                        _type = IdentityType.NoCorp;
                    }
                }

                return _type;
            }
        }

        /// <summary>
        /// 当前登录的企业ID
        /// </summary>
        public string CurrentCorpId { get; private set; }

        /// <summary>
        /// 当前公开访问仪表盘ID
        /// </summary>
        public string CurrentDashboardId { get; private set; } = string.Empty;

        /// <summary>
        /// 当前应用Id
        /// </summary>
        public string? CurrentAppId { get; private set; }

        /// <summary>
        /// 当前FormId
        /// </summary>
        public string? CurrentFormId { get; private set; }

        /// <summary>
        /// 当前的Token
        /// </summary>
        public string AccessToken { get; private set; }
        /// <summary>
        /// 当前用户对资源的访问范围
        /// </summary>
        public AccessControlLevel AccessControlLevel { get; set; } = AccessControlLevel.NotSet;

        /// <summary>
        /// 公开访问 scope（仅 Public 身份有效）
        /// </summary>
        public PublicScope PublicScope => _publicScope;
    }
}
