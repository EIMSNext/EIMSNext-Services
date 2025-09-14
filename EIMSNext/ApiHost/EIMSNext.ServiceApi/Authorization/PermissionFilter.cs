using EIMSNext.ApiService;
using EIMSNext.Cache;
using Microsoft.AspNetCore.Mvc.Filters;
using NLog;

namespace EIMSNext.ServiceApi.Authorization
{
    /// <summary>
    /// 权限过滤器
    /// </summary>
    public class PermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly ICacheClient _cache;
        //private readonly ISysUserService _userService;
        private readonly IIdentityContext _identity;
        private NLog.ILogger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="identityContext"></param>
        /// <param name="cache"></param>
        /// <param name="userService"></param>
        public PermissionFilter(IIdentityContext identityContext, ICacheClient cache)
        {
            _cache = cache;
            //_userService = userService;
            _identity = identityContext;
        }
        /// <summary>
        /// 权限校验
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            return Task.CompletedTask;

            //if (_identity.IdentityType == IdentityType.Disabled)
            //{
            //    _logger.Debug($"禁止访问:{context.HttpContext.Request.Path}, 原因：用户已被禁用");
            //    context.Result = new UnauthorizedResult();
            //    return;
            //}

            //var actionContext = (ControllerActionDescriptor)context.ActionDescriptor;
            //var perAttr = actionContext.FilterDescriptors.FirstOrDefault(f => f.Filter is PermissionAttribute)?.Filter as PermissionAttribute;

            //if (perAttr == null || perAttr.AccessControlLevel == AccessControlLevel.Allow || perAttr.AccessControlLevel == AccessControlLevel.Owner)
            //{
            //    //没有标记属性或总是允许，则不进行访问控制
            //    _identity.AccessControlLevel = perAttr == null ? AccessControlLevel.Allow : perAttr.AccessControlLevel;
            //    return;
            //}
            //else if (perAttr.AccessControlLevel == AccessControlLevel.Forbid)
            //{
            //    _logger.Debug($"禁止访问:{context.HttpContext.Request.Path}, 原因：Acl=Forbid");
            //    //如果标记为禁止访问
            //    context.Result = new ForbidResult();
            //}
            //else
            //{
            //    if (!_identity.IsAdmin) //非管理员判断权限
            //    {
            //        if (perAttr.Operation != Operation.NotSet)  //确认配置了权限控制
            //        {
            //            UserPermissions? userPermissions = null;
            //            if (!_cache.TryGetValue($"{Constants.PermissionCacheKey}{_identity.CurrentUserID}", out userPermissions) || userPermissions == null)
            //            {
            //                userPermissions = await _userService.GetFinalPermissions();
            //                _cache.Set($"{Constants.PermissionCacheKey}{_identity.CurrentUserID}", userPermissions.ApiPermission);
            //            }

            //            _logger.Debug($"userPermissions：{userPermissions!.ApiPermission.ToJson()}");
            //            var resourceCode = string.IsNullOrEmpty(perAttr.ResourceCode) ? actionContext.ControllerName.ToLower() : perAttr.ResourceCode.ToLower();
            //            var permission = userPermissions.ApiPermission.FirstOrDefault(p => p.ResourceCode.ToLower() == resourceCode);
            //            if (permission == null || !permission.Allow.HasPermission(perAttr.Operation)) //未显示资源访问权限或不具有相关权限，则禁止访问
            //            {
            //                _logger.Debug($"禁止访问:{context.HttpContext.Request.Path}, 原因：权限不足- OPT（{perAttr.Operation}），PER（{permission?.Allow}）");
            //                context.Result = new ForbidResult();
            //            }
            //        }
            //    }
            //}
        }
    }
}
