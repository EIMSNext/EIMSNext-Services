using EIMSNext.ApiService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EIMSNext.Service.Host.Authorization
{
    /// <summary>
    /// 身份过滤器
    /// </summary>
    public class IdentityTypeFilter : IAsyncAuthorizationFilter
    {
        private readonly IIdentityContext _identity;
        private readonly ILogger<IdentityTypeFilter> _logger;

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
            var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
            if (AllowAnonymous(context, actionDescriptor))
            {
                return Task.CompletedTask;
            }

            var idAttr = ResolveIdentityType(context, actionDescriptor);
            if (idAttr == null)
            {
                return Task.CompletedTask;
            }

            if (_identity.IdentityType == IdentityType.None || _identity.IdentityType == IdentityType.Disabled)
            {
                _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}", context.HttpContext.Request.Path, "无身份用户或用户已被禁用");
                context.Result = new UnauthorizedResult();
                return Task.CompletedTask;
            }

            if (!idAttr.IdentityType.HasFlag(_identity.IdentityType))
            {
                _logger.LogDebug("禁止访问 {Path}, 原因 身份不允许 - IDT({AllowedIdentityType}), USR({CurrentIdentityType})", context.HttpContext.Request.Path, idAttr.IdentityType, _identity.IdentityType);
                context.Result = new ForbidResult();
            }

            return Task.CompletedTask;
        }

        private static IdentityTypeAttribute? ResolveIdentityType(AuthorizationFilterContext context, ControllerActionDescriptor? actionDescriptor)
        {
            var actionAttr = actionDescriptor?.MethodInfo
                .GetCustomAttributes(inherit: true)
                .OfType<IdentityTypeAttribute>()
                .LastOrDefault();
            if (actionAttr != null)
            {
                return actionAttr;
            }

            var controllerAttr = actionDescriptor?.ControllerTypeInfo
                .GetCustomAttributes(inherit: true)
                .OfType<IdentityTypeAttribute>()
                .LastOrDefault();
            if (controllerAttr != null)
            {
                return controllerAttr;
            }

            return context.ActionDescriptor.EndpointMetadata
                .OfType<IdentityTypeAttribute>()
                .LastOrDefault();
        }

        private static bool AllowAnonymous(AuthorizationFilterContext context, ControllerActionDescriptor? actionDescriptor)
        {
            return context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()
                || HasActionMetadata<IAllowAnonymous>(actionDescriptor);
        }

        private static bool HasActionMetadata<TMetadata>(ControllerActionDescriptor? actionDescriptor)
        {
            return actionDescriptor?.MethodInfo.GetCustomAttributes(inherit: true).OfType<TMetadata>().Any() == true
                || actionDescriptor?.ControllerTypeInfo.GetCustomAttributes(inherit: true).OfType<TMetadata>().Any() == true;
        }
    }
}
