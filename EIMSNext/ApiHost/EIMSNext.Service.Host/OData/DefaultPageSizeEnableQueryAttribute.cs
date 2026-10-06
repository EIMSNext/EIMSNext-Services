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
            // EF Core handles null comparison semantics itself; leaving this to OData
            // wraps the IQueryable into an in-memory EnumerableQuery and silently
            // disables $filter/$orderby/$skip pushdown.
            HandleNullPropagation = HandleNullPropagationOption.False,
            PageSize = pageSize,
            TimeZone = queryOptions.Request.GetTimeZoneInfo(),
            EnableConstantParameterization = EnableConstantParameterization,
            EnsureStableOrdering = EnsureStableOrdering,
        };

        // ApplyTo's third argument is the set of options to IGNORE, not to allow.
        // Only $top=0 is treated as "no explicit top" so it falls back to the
        // default page size instead of returning zero records.
        var ignoredQueryOptions = hasPositiveTop
            ? AllowedQueryOptions.None
            : AllowedQueryOptions.Top;

        return queryOptions.ApplyTo(queryable, settings, ignoredQueryOptions);
    }
}
