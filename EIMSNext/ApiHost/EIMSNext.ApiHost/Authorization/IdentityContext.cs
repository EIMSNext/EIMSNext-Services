using EIMSNext.ApiCore;
using EIMSNext.ApiService;
using EIMSNext.Auth.Entity;
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;

using HKH.Mef2.Integration;

using IdentityModel;

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
        private IResolver _resolver;
        private User? _user;
        private Employee? _employee;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver"></param>
        public IdentityContext(IResolver resolver)
        {
            _resolver = resolver;
            IHttpContextAccessor httpContextAccessor = resolver.Resolve<IHttpContextAccessor>();
            AccessToken = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault() ?? "";
            var idClaim = httpContextAccessor.HttpContext?.User.FindFirst(JwtClaimTypes.Id);
            var corpClaim = httpContextAccessor.HttpContext?.User.FindFirst("corp");
            CurrentUserID = idClaim?.Value ?? string.Empty;
            CurrentCorpId = corpClaim?.Value ?? string.Empty;

            if (idClaim == null && corpClaim == null)
            {
                var client_idClaim = httpContextAccessor.HttpContext?.User.FindFirst("client_id");
                var clientId = client_idClaim?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(clientId))
                {
                    var client = resolver.GetService<Auth.Entity.Client>().Get(clientId);
                    if (client != null)
                    {
                        CurrentCorpId = client.CorpId;
                        CurrentUserID = "system";
                    }
                }
            }

            var serviceContext = resolver.GetServiceContext();
            serviceContext.AccessToken = AccessToken;
            serviceContext.CorpId = CurrentCorpId;
            serviceContext.User = CurrentUser;
            serviceContext.Employee = CurrentEmployee;
            serviceContext.Operator = CurrentEmployee?.ToOperator() ?? Operator.Empty;
            serviceContext.ClientIp = IpHelper.GetClientIp(httpContextAccessor);
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
                    _employee = _resolver.GetRepository<Employee>().Queryable.FirstOrDefault(x => x.CorpId == CurrentCorpId && x.UserId == _user.Id);
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
                            else
                            {
                                //TODO:将来根据角色来指定身份
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
    }
}
