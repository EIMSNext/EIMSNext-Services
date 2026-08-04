using EIMSNext.ApiService;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

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
        private readonly AdminPermissionEvaluator _permissionEvaluator;
        private readonly ILogger<PermissionFilter> _logger;

        public PermissionFilter(
            IIdentityContext identityContext,
            ICacheClient cache,
            IPublicAccessValidator publicAccessValidator,
            AdminPermissionEvaluator permissionEvaluator,
            ILogger<PermissionFilter> logger)
        {
            _identity = identityContext;
            _cache = cache;
            _publicAccessValidator = publicAccessValidator;
            _permissionEvaluator = permissionEvaluator;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
            if (AllowAnonymous(context, actionDescriptor))
            {
                return;
            }

            var permission = ResolvePermission(context, actionDescriptor);
            var requiresAuthorization = RequiresAuthorization(context, actionDescriptor);
            if (permission == null && !requiresAuthorization)
            {
                return;
            }

            if (_identity.IdentityType == IdentityType.None || _identity.IdentityType == IdentityType.Disabled)
            {
                _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}", context.HttpContext.Request.Path, "无身份用户或用户已被禁用");
                context.Result = new UnauthorizedResult();
                return;
            }

            if (_identity.IdentityType == IdentityType.Public)
            {
                if (permission == null || permission.AccessControlLevel == AccessControlLevel.Forbid)
                {
                    _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}", context.HttpContext.Request.Path, "公开接口缺少权限标记或显式禁止");
                    context.Result = new ForbidResult();
                return;
                }

                if (!_publicAccessValidator.IsAnySectionEnabled())
                {
                    _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}", context.HttpContext.Request.Path, "公开资源未启用任何 section");
                    context.Result = new ForbidResult();
                return;
                }

                var requiredScope = ResolvePublicScope(context, actionDescriptor);
                // A public token carries exactly one scope. Controller metadata may
                // combine several scopes to allow any of those public link types.
                if (requiredScope != PublicScope.None && (_identity.PublicScope & requiredScope) == PublicScope.None)
                {
                    _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}, RequiredScope={Required}, TokenScope={Token}",
                        context.HttpContext.Request.Path,
                        "公开 scope 不足",
                        requiredScope,
                        _identity.PublicScope);
                    context.Result = new ForbidResult();
                return;
                }

                _identity.AccessControlLevel = permission.AccessControlLevel;
                return;
            }

            if (permission == null || permission.AccessControlLevel == AccessControlLevel.Allow || permission.AccessControlLevel == AccessControlLevel.Owner)
            {
                _identity.AccessControlLevel = permission == null ? AccessControlLevel.Allow : permission.AccessControlLevel;
                return;
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

            if (permission != null && !await HasActionPermissionAsync(context, permission))
            {
                _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}, ResourceCode={ResourceCode}, Operation={Operation}",
                    context.HttpContext.Request.Path,
                    "缺少接口权限标识",
                    permission.ResourceCode,
                    permission.Operation);
                context.Result = new ForbidResult();
            }

            return;
        }

        private async Task<bool> HasActionPermissionAsync(AuthorizationFilterContext context, PermissionAttribute permission)
        {
            // 只有当 ResourceCode 和 Operation 都未设置时才完全跳过检查；
            // 只要其中任一被显式标注，就必须进入匹配。
            if (string.IsNullOrWhiteSpace(permission.ResourceCode) && permission.Operation == Operation.NotSet)
            {
                return true;
            }

            if (_identity.IdentityType == IdentityType.System ||
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

            // Client (client_credentials grant) 走 ClientGrant 缓存
            if (_identity.IdentityType == IdentityType.Client)
            {
                var clientId = context.HttpContext.User.Claims
                    .FirstOrDefault(c => string.Equals(c.Type, EIMSNext.Auth.Entities.AuthClaimTypes.ClientId, StringComparison.OrdinalIgnoreCase))?.Value;
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    return false;
                }
                var clientInfo = _cache.Get<EIMSNext.Service.Host.OpenPlatform.ClientPermissionInfo>(
                    "clientGrant", CacheScope.Client, clientId);

                // 缓存未命中：lazy refresh（避免重启 Auth.Host 后第一次 token 失败）
                if (clientInfo == null)
                {
                    clientInfo = TryLazyRefreshClientInfo(context, clientId);
                }

                if (clientInfo == null)
                {
                    return false;
                }

                // Client / Grant 启用检查
                if (!clientInfo.ClientEnabled)
                {
                    _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}, ClientId={ClientId}",
                        context.HttpContext.Request.Path, "Client 已禁用", clientId);
                    return false;
                }
                if (!clientInfo.GrantEnabled)
                {
                    _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}, ClientId={ClientId}",
                        context.HttpContext.Request.Path, "ClientGrant 已禁用", clientId);
                    return false;
                }

                // IP 白名单检查（非空且不包含则拒绝）
                if (clientInfo.IpWhitelist != null && clientInfo.IpWhitelist.Count > 0)
                {
                    var httpAccessor = context.HttpContext.RequestServices
                        .GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                    var clientIp = httpAccessor != null
                        ? EIMSNext.ApiCore.IpHelper.GetClientIp(httpAccessor)
                        : string.Empty;
                    if (string.IsNullOrEmpty(clientIp) || !clientInfo.IpWhitelist.Any(ip => IpMatches(ip.Trim(), clientIp)))
                    {
                        _logger.LogDebug("禁止访问 {Path}, 原因 {Reason}, ClientId={ClientId}, ClientIp={ClientIp}",
                            context.HttpContext.Request.Path, "Client IP 不在白名单", clientId, clientIp);
                        return false;
                    }
                }

                if (clientInfo.Codes == null || clientInfo.Codes.Count == 0)
                {
                    return false;
                }
                return requiredCodes.Any(code => clientInfo.Codes.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase)));
            }

            var userCodes = ResolveUserPermissionCodes(context).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (requiredCodes.Any(userCodes.Contains))
            {
                return true;
            }

            if (string.Equals(permission.ResourceCode, Resources.FormData, StringComparison.OrdinalIgnoreCase))
            {
                return await HasFormDataPermissionAsync(context, permission.Operation);
            }

            return false;
        }

        private async Task<bool> HasFormDataPermissionAsync(AuthorizationFilterContext context, Operation operation)
        {
            if (_identity.CurrentEmployee == null)
            {
                return false;
            }

            var formId = await ReadFormIdAsync(context.HttpContext.Request);
            if (string.IsNullOrWhiteSpace(formId))
            {
                return false;
            }

            var required = operation.HasFlag(Operation.Add) ? DataPerms.AddNew :
                operation.HasFlag(Operation.Edit) ? DataPerms.Edit :
                operation.HasFlag(Operation.Delete) ? DataPerms.Remove :
                operation.HasFlag(Operation.Import) ? DataPerms.Import :
                operation.HasFlag(Operation.Read) ? DataPerms.View : DataPerms.None;

            if (required == DataPerms.None)
            {
                return false;
            }

            return _permissionEvaluator.GetUsageAuthGroupsForCurrentEmployee(formId)
                .Any(group => (GetEffectiveDataPerms(group) & required) == required);
        }

        private static async Task<string?> ReadFormIdAsync(HttpRequest request)
        {
            if (request.ContentLength is null or 0 || request.Body == Stream.Null)
            {
                return null;
            }

            request.EnableBuffering();
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var json = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, "formId", StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        private static DataPerms GetEffectiveDataPerms(AuthGroup group)
        {
            return group.Type switch
            {
                AuthGroupType.ManageSelfData or AuthGroupType.ManageAllData => DataPerms.All,
                AuthGroupType.ViewAllData => DataPerms.View,
                _ => (DataPerms)group.DataPerms,
            };
        }

        private static IEnumerable<string> BuildPermissionCodes(PermissionAttribute permission)
        {
            if (string.IsNullOrWhiteSpace(permission.ResourceCode))
            {
                yield break;
            }

            var resourceCode = permission.ResourceCode.Trim();
            yield return resourceCode;

            if (permission.Operation.HasFlag(Operation.Read))
            {
                yield return $"{resourceCode}:read";
            }

            if (permission.Operation.HasFlag(Operation.Add))
            {
                yield return $"{resourceCode}:add";
            }

            if (permission.Operation.HasFlag(Operation.Edit))
            {
                yield return $"{resourceCode}:edit";
            }

            if (permission.Operation.HasFlag(Operation.Delete))
            {
                yield return $"{resourceCode}:delete";
            }

            if (permission.Operation.HasFlag(Operation.Import))
            {
                yield return $"{resourceCode}:import";
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

        /// <summary>
        /// 缓存未命中时，尝试从 DB 拉一次 ClientGrant 重建缓存。
        /// 仅供 Client 路径使用。
        /// </summary>
        private EIMSNext.Service.Host.OpenPlatform.ClientPermissionInfo?
            TryLazyRefreshClientInfo(AuthorizationFilterContext context, string clientId)
        {
            try
            {
                var grantApi = (EIMSNext.ApiService.ClientGrantApiService?)
                    context.HttpContext.RequestServices.GetService(typeof(EIMSNext.ApiService.ClientGrantApiService));
                var clientApi = (EIMSNext.ApiService.ClientApiService?)
                    context.HttpContext.RequestServices.GetService(typeof(EIMSNext.ApiService.ClientApiService));
                if (grantApi == null || clientApi == null) return null;

                // 同步等待（这里在 IAsyncAuthorizationFilter.OnAuthorizationAsync 内，本身就是异步上下文）
                EIMSNext.Service.Host.OpenPlatform.ClientPermissionCache
                    .RefreshAsync(_cache, grantApi, clientApi, _identity.CurrentCorpId, clientId)
                    .GetAwaiter()
                    .GetResult();
                return _cache.Get<EIMSNext.Service.Host.OpenPlatform.ClientPermissionInfo>(
                    "clientGrant", CacheScope.Client, clientId);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 判断 <paramref name="clientIp"/> 是否匹配 <paramref name="rule"/>。
        /// 由 <see cref="EIMSNext.Common.IpMatcher"/> 提供：支持精确 IP、通配符 <c>10.0.0.*</c> 与 CIDR <c>10.0.0.0/24</c>。
        /// </summary>
        private static bool IpMatches(string rule, string clientIp)
            => EIMSNext.Common.IpMatcher.Matches(rule, clientIp);

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
