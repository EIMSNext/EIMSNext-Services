using EIMSNext.ApiService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EIMSNext.ServiceApi.Authorization
{
    /// <summary>
    /// 身份过滤器
    /// </summary>
    public class IdentityTypeFilter : IAsyncAuthorizationFilter
    {
        private readonly IIdentityContext _identity;
        private ILogger<IdentityTypeFilter> _logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="identityContext"></param>
        /// <param name="logger"></param>
        public IdentityTypeFilter(IIdentityContext identityContext, ILogger<IdentityTypeFilter> logger)
        {
            _identity = identityContext;
            _logger = logger;
        }
        /// <summary>
        /// 权限校验
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (_identity.IdentityType== IdentityType.None || _identity.IdentityType == IdentityType.Disabled)
            {
                _logger.LogDebug($"禁止访问:{context.HttpContext.Request.Path}, 原因：无身份用户或用户已被禁用");
                context.Result = new UnauthorizedResult();
                return Task.CompletedTask;
            }

            var actionContext = (ControllerActionDescriptor)context.ActionDescriptor;
            var idAttr = actionContext.FilterDescriptors.FirstOrDefault(f => f.Filter is IdentityTypeAttribute)?.Filter as IdentityTypeAttribute;

            if (idAttr != null)
            {                
                //if (_identity.HasCorpAdminPermission()) //不具备超管级别权限
                //{
                    if (!idAttr.IdentityType.HasFlag(_identity.IdentityType))
                    {
                        _logger.LogDebug($"禁止访问:{context.HttpContext.Request.Path}, 原因：身份不允许 - IDT（{idAttr.IdentityType}），USR（{_identity.IdentityType}）");
                        //如果配置了明确身份限制并且不包含超管理员，则禁止访问
                        context.Result = new ForbidResult();
                    }
                //}
                //else
                //{
                //    if (!idAttr.IdentityType.HasFlag(_identity.IdentityType))
                //    {
                //        _logger.LogDebug($"禁止访问:{context.HttpContext.Request.Path}, 原因：身份不允许 - IDT（{idAttr.IdentityType}），USR（{_identity.IdentityType}）");
                //        //如果当前用户没有身价，或者配置了身份限制并且不包含当前身份，则禁止访问
                //        context.Result = new ForbidResult();
                //    }
                //}
            }

            return Task.CompletedTask;
        }
    }
}