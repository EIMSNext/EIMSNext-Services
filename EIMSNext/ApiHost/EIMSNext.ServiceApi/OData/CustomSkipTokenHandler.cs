using System.Text;
using System.Web;
using EIMSNext.Common;
using Microsoft.AspNetCore.OData.Formatter.Serialization;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.ServiceApi.OData
{
    /// <summary>
    /// 
    /// </summary>
    public class CustomSkipTokenHandler : DefaultSkipTokenHandler
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="skipTokenQueryOption"></param>
        /// <param name="querySettings"></param>
        /// <param name="queryOptions"></param>
        /// <returns></returns>
        public override IQueryable<T> ApplyTo<T>(IQueryable<T> query, SkipTokenQueryOption skipTokenQueryOption, ODataQuerySettings querySettings, ODataQueryOptions queryOptions)
        {
            var decodedSkipTokenQueryOption = DecodeSkipToken(skipTokenQueryOption);
            return base.ApplyTo(query, decodedSkipTokenQueryOption, querySettings, queryOptions);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="skipTokenQueryOption"></param>
        /// <param name="querySettings"></param>
        /// <param name="queryOptions"></param>
        /// <returns></returns>
        public override IQueryable ApplyTo(IQueryable query, SkipTokenQueryOption skipTokenQueryOption, ODataQuerySettings querySettings, ODataQueryOptions queryOptions)
        {
            var decodedSkipTokenQueryOption = DecodeSkipToken(skipTokenQueryOption);
            return base.ApplyTo(query, decodedSkipTokenQueryOption, querySettings, queryOptions);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseUri"></param>
        /// <param name="pageSize"></param>
        /// <param name="instance"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Uri? GenerateNextPageLink(
            Uri baseUri,
            int pageSize,
            object instance,
            ODataSerializerContext context)
        {
            pageSize = (pageSize < 1 ? Constants.DefaultPageSize : pageSize);
            var uri = base.GenerateNextPageLink(baseUri, pageSize, instance, context);

            if (uri == null)
            {
                return null;
            }

            var queryOptions = HttpUtility.ParseQueryString(uri.Query)!;
            string? skipToken = queryOptions.Get("$skiptoken");
            if (skipToken == null)
            {
                return uri;
            }

            string base64SkipToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(skipToken));
            queryOptions.Set("$skiptoken", base64SkipToken);

            string encodedQuery = queryOptions.ToString()!;
            UriBuilder builder = new UriBuilder(uri);
            builder.Query = encodedQuery;
            return builder.Uri;
        }

        private static SkipTokenQueryOption DecodeSkipToken(SkipTokenQueryOption skipTokenQueryOption)
        {
            string encodedSkipToken = skipTokenQueryOption.RawValue;
            string decodedSkipToken = Encoding.UTF8.GetString(Convert.FromBase64String(encodedSkipToken));
            return new SkipTokenQueryOption(decodedSkipToken, skipTokenQueryOption.Context);
        }
    }
}
