using EIMSNext.ApiService;
using EIMSNext.Cache;
using EIMSNext.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EIMSNext.Service.Host.Authorization
{
    /// <summary>
    /// 权限过滤器
    /// </summary>
    public class PermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly ICacheClient _cache;
        private readonly IIdentityContext _identity;
        private readonly IPublicAccessValidator _publicAccessValidator;
        private readonly ILogger<PermissionFilter> _logger;

        public PermissionFilter(
            IIdentityContext identityContext,
            ICacheClient cache,
            IPublicAccessValidator publicAccessValidator,
            ILogger<PermissionFilter> logger)
        {
            _identity = identityContext;
            _cache = cache;
            _publicAccessValidator = publicAccessValidator;
            _logger = logger;
        }

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
            if (AllowAnonymous(context, actionDescriptor))
            {
                return Task.CompletedTask;
            }

            var permission = ResolvePermission(context, actionDescriptor);
            var requiresAuthorization = RequiresAuthorization(context, actionDescriptor);
            if (permission == null && !requiresAuthorization)
            {
                return Task.CompletedTask;
            }

            if (_identity.IdentityType == IdentityType.None || _identity.IdentityType == IdentityType.Disabled)
            {
                _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}", context.HttpContext.Request.Path, "无身份用户或用户已被禁用");
                context.Result = new UnauthorizedResult();
                return Task.CompletedTask;
            }

            if (_identity.IdentityType == IdentityType.Public)
            {
                if (permission == null || permission.AccessControlLevel == AccessControlLevel.Forbid)
                {
                    _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}", context.HttpContext.Request.Path, "公开接口缺少权限标记或显式禁止");
                    context.Result = new ForbidResult();
                    return Task.CompletedTask;
                }

                if (!_publicAccessValidator.IsAnySectionEnabled())
                {
                    _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}", context.HttpContext.Request.Path, "公开资源未启用任何 section");
                    context.Result = new ForbidResult();
                    return Task.CompletedTask;
                }

                var requiredScope = ResolvePublicScope(context, actionDescriptor);
                if (requiredScope != PublicScope.None && (_identity.PublicScope & requiredScope) != requiredScope)
                {
                    _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}, RequiredScope={Required}, TokenScope={Token}",
                        context.HttpContext.Request.Path,
                        "公开 scope 不足",
                        requiredScope,
                        _identity.PublicScope);
                    context.Result = new ForbidResult();
                    return Task.CompletedTask;
                }

                _identity.AccessControlLevel = permission.AccessControlLevel;
                return Task.CompletedTask;
            }

            if (permission == null || permission.AccessControlLevel == AccessControlLevel.Allow || permission.AccessControlLevel == AccessControlLevel.Owner)
            {
                _identity.AccessControlLevel = permission == null ? AccessControlLevel.Allow : permission.AccessControlLevel;
                return Task.CompletedTask;
            }
            else if (permission.AccessControlLevel == AccessControlLevel.Forbid)
            {
                _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}", context.HttpContext.Request.Path, "Acl=Forbid");
                context.Result = new ForbidResult();
            }
            else
            {
                _identity.AccessControlLevel = permission.AccessControlLevel;
            }

            if (permission != null && !HasActionPermission(context, permission))
            {
                _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}, ResourceCode={ResourceCode}, Operation={Operation}",
                    context.HttpContext.Request.Path,
                    "缺少接口权限标识",
                    permission.ResourceCode,
                    permission.Operation);
                context.Result = new ForbidResult();
            }

            return Task.CompletedTask;
        }

        private bool HasActionPermission(AuthorizationFilterContext context, PermissionAttribute permission)
        {
            if (string.IsNullOrWhiteSpace(permission.ResourceCode) || permission.Operation == Operation.NotSet)
            {
                return true;
            }

            if (_identity.IdentityType == IdentityType.System ||
                _identity.IdentityType == IdentityType.Client ||
                _identity.IdentityType == IdentityType.CorpOwmer ||
                _identity.IdentityType == IdentityType.CorpAdmin)
            {
                return true;
            }

            var requiredCodes = BuildPermissionCodes(permission).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (requiredCodes.Count == 0)
            {
                return true;
            }

            var userCodes = ResolveUserPermissionCodes(context).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return requiredCodes.Any(userCodes.Contains);
        }

        private static IEnumerable<string> BuildPermissionCodes(PermissionAttribute permission)
        {
            var resourceCode = permission.ResourceCode.Trim();
            yield return resourceCode;

            if (permission.Operation.HasFlag(Operation.Read))
            {
                yield return $"{resourceCode}:read";
            }

            if (permission.Operation.HasFlag(Operation.Write))
            {
                yield return $"{resourceCode}:write";
            }
        }

        private IEnumerable<string> ResolveUserPermissionCodes(AuthorizationFilterContext context)
        {
            foreach (var claim in context.HttpContext.User.Claims)
            {
                if (!IsPermissionClaim(claim.Type))
                {
                    continue;
                }

                foreach (var code in SplitPermissionCodes(claim.Value))
                {
                    yield return code;
                }
            }

            var currentEmployeeId = _identity.CurrentEmployee?.Id;
            if (string.IsNullOrWhiteSpace(currentEmployeeId))
            {
                yield break;
            }

            var cached = _cache.Get<List<string>>("permissions", CacheScope.Employee, currentEmployeeId);
            if (cached == null)
            {
                yield break;
            }

            foreach (var code in cached)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    yield return code;
                }
            }
        }

        private static bool IsPermissionClaim(string type)
        {
            return string.Equals(type, "perm", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "perms", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "permission", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "permissions", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> SplitPermissionCodes(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (var code in value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return code;
            }
        }

        private static PermissionAttribute? ResolvePermission(AuthorizationFilterContext context, ControllerActionDescriptor? actionDescriptor)
        {
            var actionPermission = actionDescriptor?.MethodInfo
                .GetCustomAttributes(inherit: true)
                .OfType<PermissionAttribute>()
                .LastOrDefault();
            if (actionPermission != null)
            {
                return actionPermission;
            }

            var controllerPermission = actionDescriptor?.ControllerTypeInfo
                .GetCustomAttributes(inherit: true)
                .OfType<PermissionAttribute>()
                .LastOrDefault();
            if (controllerPermission != null)
            {
                return controllerPermission;
            }

            return context.ActionDescriptor.EndpointMetadata
                .OfType<PermissionAttribute>()
                .LastOrDefault();
        }

        private static bool RequiresAuthorization(AuthorizationFilterContext context, ControllerActionDescriptor? actionDescriptor)
        {
            return context.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any()
                || HasActionMetadata<IAuthorizeData>(actionDescriptor);
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

        private static PublicScope ResolvePublicScope(AuthorizationFilterContext context, ControllerActionDescriptor? actionDescriptor)
        {
            var actionAttr = actionDescriptor?.MethodInfo
                .GetCustomAttributes(inherit: true)
                .OfType<PublicScopeAttribute>()
                .LastOrDefault();
            if (actionAttr != null) return actionAttr.Scope;

            var controllerAttr = actionDescriptor?.ControllerTypeInfo
                .GetCustomAttributes(inherit: true)
                .OfType<PublicScopeAttribute>()
                .LastOrDefault();
            if (controllerAttr != null) return controllerAttr.Scope;

            return PublicScope.None;
        }
    }
}
