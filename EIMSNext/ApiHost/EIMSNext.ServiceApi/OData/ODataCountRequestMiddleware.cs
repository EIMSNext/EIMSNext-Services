using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.ServiceApi.OData;

/// <summary>
/// Defines the middleware for handling OData $count requests.
/// This middleware essentially transforms the incoming request (Post) to a "Get" request.
/// Be noted: should put this middle ware before "UseRouting()".
/// </summary>
public class ODataCountRequestMiddleware
{
    private const string CountSegment = "$count";
    private const string RequiredContentType = "text/plain";
    private IEnumerable<IODataQueryRequestParser> _parsers;
    private readonly RequestDelegate _next;

    /// <summary>
    /// Instantiates a new instance of <see cref="ODataQueryRequestMiddleware"/>.
    /// </summary>
    /// <param name="queryRequestParsers">The query request parsers.</param>
    /// <param name="next">The next middleware.</param>
    public ODataCountRequestMiddleware(IEnumerable<IODataQueryRequestParser> queryRequestParsers, RequestDelegate next)
    {
        _parsers = queryRequestParsers;
        _next = next;
    }

    /// <summary>
    /// Invoke the OData $query middleware.
    /// </summary>
    /// <param name="context">The http context.</param>
    /// <returns>A task that can be awaited.</returns>
    public async Task Invoke(HttpContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        HttpRequest request = context.Request;
        if (IsValidPostCountRequest(request))
        {
            foreach (IODataQueryRequestParser parser in _parsers)
            {
                if (parser.CanParse(request))
                {
                    await TransformQueryRequestAsync(parser, request).ConfigureAwait(false);
                    break;
                }
            }
        }

        await _next(context).ConfigureAwait(false);
    }
    private bool IsValidPostCountRequest(HttpRequest request)
    {
        // 验证三要素：POST方法、$count路径、text/plain类型
        return !string.IsNullOrEmpty(request.Path.Value) && !string.IsNullOrEmpty(request.ContentType)
            && request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && request.Path.Value.EndsWith(CountSegment, StringComparison.OrdinalIgnoreCase)
            && request.ContentType.StartsWith(RequiredContentType, StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>
    /// Transforms a POST request targeted at a resource path ending in $count into a GET request.
    /// The query options are parsed from the request body and appended to the request URL.
    /// </summary>
    /// <param name="parser">The query request parser.</param>
    /// <param name="request">The Http request.</param>
    internal static async Task TransformQueryRequestAsync(IODataQueryRequestParser parser, HttpRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Parse query options in request body
        string queryOptions = await parser.ParseAsync(request).ConfigureAwait(false);

        var requestPath = request.Path.Value?.TrimEnd('/');
        var queryString = request.QueryString.Value;

        if (!string.IsNullOrWhiteSpace(queryOptions))
        {
            if (string.IsNullOrWhiteSpace(queryString))
            {
                queryString = '?' + queryOptions;
            }
            else
            {
                queryString += '&' + queryOptions;
            }
        }

        request.Path = new PathString(requestPath);
        request.QueryString = new QueryString(queryString);
        request.Method = HttpMethods.Get;
    }
}
