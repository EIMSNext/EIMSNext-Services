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
    public abstract class IdentityContext : IIdentityContext
    {
        private bool _retrieved = false;
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

            //if (idClaim == null && corpClaim == null)
            //{
            //    var client_idClaim = httpContextAccessor.HttpContext?.User.FindFirst("client_id");
            //    var clientId = client_idClaim?.Value ?? string.Empty;
            //    if (!string.IsNullOrEmpty(clientId))
            //    {
            //        var client = resolver.GetService<Client>().Get(clientId);
            //        if (client != null)
            //        {
            //            CurrentCorpId = client.CorpId;
            //            CurrentUserID = "system";
            //        }
            //    }
            //}

            //var httpQuery = httpContextAccessor.HttpContext?.Request.Query;
            //if (httpQuery != null)
            //{
            //    CurrentAppId = httpQuery.FirstOrDefault(x => x.Key.EqualsIgnoreCase("appid")).Value;
            //    CurrentFormId = httpQuery.FirstOrDefault(x => x.Key.EqualsIgnoreCase("formid")).Value;
            //}

            var serviceContext = resolver.GetServiceContext();
            //serviceContext.User = CurrentUser;
            serviceContext.Employee = CurrentEmployee;
            serviceContext.Operator = CurrentEmployee?.ToOperator() ?? Operator.Empty;
            serviceContext.AccessToken = AccessToken;
            serviceContext.CorpId = CurrentCorpId;
        }

        /// <summary>
        /// 当前用户ID
        /// </summary>
        public string CurrentUserID { get; private set; } = string.Empty;

        public IEmployee CurrentEmployee
        {
            get
            {
                if (!_retrieved && _employee == null)
                    Retrieve();
                return _employee!;
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

        public virtual IUser? CurrentUser => throw new NotImplementedException();

        public virtual ApiService.IdentityType IdentityType => throw new NotImplementedException();

        public string AccessToken { get; private set; }
    }
}
