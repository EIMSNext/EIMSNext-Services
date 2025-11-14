using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;
using HKH.Mef2.Integration;
using IdentityModel;

namespace EIMSNext.FileUploadApi.Authorization
{
    /// <summary>
    /// 身份标识上下文
    /// </summary>
    public class IdentityContext : IIdentityContext
    {
        private bool _retrieved = false;
        private IEmployee? _employee;

        private IResolver _resolver;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver"></param>
        public IdentityContext(IResolver resolver)
        {
            _resolver = resolver;
            IHttpContextAccessor httpContextAccessor = resolver.Resolve<IHttpContextAccessor>();
            var idClaim = httpContextAccessor.HttpContext?.User.FindFirst(JwtClaimTypes.Id);
            var corpClaim = httpContextAccessor.HttpContext?.User.FindFirst("corp");
            CurrentUserID = idClaim?.Value ?? string.Empty;
            CurrentCorpId = corpClaim?.Value ?? string.Empty;

            var serviceContext = resolver.GetServiceContext();
            serviceContext.Employee = CurrentEmployee;
            serviceContext.Operator = CurrentEmployee?.ToOperator() ?? Operator.Empty;
            serviceContext.AccessToken = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault() ?? "";
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
                _employee = _resolver.GetRepository<Employee>().Queryable.FirstOrDefault(x => x.CorpId == CurrentCorpId && x.UserId == CurrentUserID);
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
    }
}
