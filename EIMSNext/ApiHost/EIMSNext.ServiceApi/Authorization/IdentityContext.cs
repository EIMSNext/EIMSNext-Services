using EIMSNext.ApiService;
using EIMSNext.Auth.Entity;
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;
using HKH.Mef2.Integration;
using IdentityModel;

namespace EIMSNext.ServiceApi.Authorization
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

            var httpQuery = httpContextAccessor.HttpContext?.Request.Query;
            if (httpQuery != null)
            {
                CurrentAppId = httpQuery.FirstOrDefault(x => x.Key.EqualsIgnoreCase("appid")).Value;
                CurrentFormId = httpQuery.FirstOrDefault(x => x.Key.EqualsIgnoreCase("formid")).Value;
            }

            var serviceContext = resolver.GetServiceContext();
            serviceContext.AccessToken = AccessToken;
            serviceContext.CorpId = CurrentCorpId;
            serviceContext.User = CurrentUser;
            serviceContext.Employee = CurrentEmployee;
            serviceContext.Operator = CurrentEmployee?.ToOperator() ?? Operator.Empty;
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
                if (_type == IdentityType.None)
                {
                    if (CurrentUser != null)
                    {
                        var corp = CurrentUser.AsUser().Crops.FirstOrDefault(x => x.CorpId == CurrentCorpId);
                        if (corp != null)
                        {
                            if (corp.IsCorpOwner) _type = IdentityType.CorpOwmer;
                            else
                            {

                            }
                        }
                    }
                }
                //if (_type == IdentityType.None)
                //{
                //    if (IsAdmin)
                //    {
                //        _type = IdentityType.Admin;
                //    }
                //    else
                //    {
                //        _user = CurrentUser;
                //        if (_user != null)
                //        {
                //            if (_user.Disabled ?? false)
                //            {
                //                _type = IdentityType.Disabled;
                //            }
                //            else
                //            {
                //                if (_user.UserType == null)
                //                    _type = IdentityType.NonRegister;
                //                else if (_user.UserType == 1)
                //                    _type = IdentityType.Terminal;
                //                else
                //                {
                //                    _type = IdentityType.Internal;

                //                    var _org = _user.SysOrg;
                //                    if (_org != null)
                //                    {
                //                        if (_org.IsMerchant.HasValue && _org.IsMerchant.Value)
                //                            _type = IdentityType.Agent;
                //                        if (_org.IsTransport.HasValue && _org.IsTransport.Value)
                //                            _type = IdentityType.Transport;
                //                        if (_org.IsRecovery.HasValue && _org.IsRecovery.Value)
                //                            _type = IdentityType.Recovery;
                //                    }
                //                }
                //            }
                //        }
                //    }
                //}

                return _type;
            }
        }

        /// <summary>
        /// 当前用户对资源的访问范围
        /// </summary>
        //public AccessControlLevel AccessControlLevel { get; set; } = AccessControlLevel.NotSet;

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
    }

    /// <summary>
    /// 
    /// </summary>
    public static class IdentityExtension
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static User AsUser(this IUser user)
        {
            return (User)user;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="emp"></param>
        /// <returns></returns>
        public static Employee AsEmployee(this IEmployee emp)
        {
            return (Employee)emp;
        }
    }
}
