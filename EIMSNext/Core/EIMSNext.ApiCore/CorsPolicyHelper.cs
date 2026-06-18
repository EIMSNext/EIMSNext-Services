using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace EIMSNext.ApiCore
{
    public class CorsPolicyHelper
    {
        public const string AllowedMethods = "PUT,POST,GET,DELETE,OPTIONS,HEAD,PATCH";
        public const string AllowedHeaders = "Authorization,Content-Type,Accept,Origin,X-Requested-With";
        private readonly CorsOptions _options;

        public CorsPolicyHelper(IOptions<CorsOptions> options)
        {
            _options = options.Value;
        }

        public bool Apply(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue(CorsConstants.Origin, out var originValues))
            {
                return false;
            }

            var origin = originValues.ToString();
            if (!IsAllowedOrigin(origin))
            {
                return false;
            }

            context.Response.Headers.AccessControlAllowOrigin = origin;
            context.Response.Headers.AccessControlAllowMethods = AllowedMethods;
            context.Response.Headers.AccessControlAllowHeaders =
                context.Request.Headers.AccessControlRequestHeaders.Count > 0
                    ? context.Request.Headers.AccessControlRequestHeaders
                    : AllowedHeaders;

            return true;
        }

        private bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return _options.AllowedOrigins.Any(allowed => IsOriginMatch(uri, allowed));
        }

        private static bool IsOriginMatch(Uri origin, string allowed)
        {
            if (string.IsNullOrWhiteSpace(allowed))
            {
                return false;
            }

            if (allowed.EndsWith(":*", StringComparison.Ordinal))
            {
                var baseOrigin = allowed[..^2];
                return Uri.TryCreate(baseOrigin, UriKind.Absolute, out var allowedUri)
                    && string.Equals(origin.Scheme, allowedUri.Scheme, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(origin.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(origin.GetLeftPart(UriPartial.Authority), allowed.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
