using EIMSNext.Common;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.Service.Host.OData;

/// <summary>
/// Uses the default page size only when the request does not ask for a positive $top.
/// An explicit $top remains subject to the global MaxTop limit.
/// </summary>
public sealed class DefaultPageSizeEnableQueryAttribute : EnableQueryAttribute
{
    public override IQueryable ApplyQuery(IQueryable queryable, ODataQueryOptions queryOptions)
    {
        // $count has a raw-value response contract. Let EnableQueryAttribute
        // keep its built-in count handling instead of returning an IQueryable.
        if (queryOptions.Request.IsCountRequest())
        {
            return base.ApplyQuery(queryable, queryOptions);
        }

        var hasPositiveTop = queryOptions.Top?.Value > 0;
        int? pageSize = hasPositiveTop ? null : Constants.DefaultPageSize;
        var settings = new ODataQuerySettings
        {
            PageSize = pageSize,
        };

        // Treat an explicit $top=0 as the default page-size request instead of
        // letting OData interpret it as "return zero records".
        var allowedQueryOptions = hasPositiveTop
            ? AllowedQueryOptions.All
            : AllowedQueryOptions.All & ~AllowedQueryOptions.Top;

        return queryOptions.ApplyTo(queryable, settings, allowedQueryOptions);
    }
}
