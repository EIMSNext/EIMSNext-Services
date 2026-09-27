using EIMSNext.ApiService;

namespace EIMSNext.File.Host;

public sealed class FileAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IIdentityContext identityContext)
    {
        // 文件访问控制暂时关闭，保留下面的实现供后续重新启用。
        await next(context);
        return;

        /*
        var requestPath = context.Request.Path.Value ?? string.Empty;
        if (!FileAccessSigner.TryGetPathCorpId(requestPath, out var pathCorpId))
        {
            await next(context);
            return;
        }

        var path = FileAccessSigner.NormalizePath(requestPath)!;
        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(identityContext.CurrentCorpId))
        {
            if (!string.Equals(identityContext.CurrentCorpId, pathCorpId, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
            return;
        }

        if (signer.ValidateSignedRequest(context.Request, path, pathCorpId))
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        */
    }
}
