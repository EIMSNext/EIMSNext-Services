using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public sealed class PublicFormLinkGuard(IResolver resolver) : ApiServiceBase(resolver)
    {
        private const string WxOpenIdFieldPath = "data.wxopenid";
        private const string CreateByIpFieldPath = "createBy.id";
        private static readonly TimeSpan OneSubmitWindow = TimeSpan.FromDays(1);

        public void EnsureCanSubmit(PublicFormLinkSetting setting, FormData draft, string? wxOpenId, string ip, string? corpId, string? formId)
        {
            if (!setting.Enabled)
            {
                throw new InvalidOperationException("form_link_disabled");
            }

            if (!setting.OneSubmit)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(corpId) || string.IsNullOrWhiteSpace(formId))
            {
                return;
            }

            var (field, value) = ResolveDedupKey(setting, wxOpenId, ip, corpId, formId);
            var filter = BuildDupFilter(corpId, formId, field, value);

            var collection = Resolver.Resolve<IFormDataService>().Collection;
            var count = collection.CountDocuments(filter.ToFilterDefinition<FormData>(), new CountOptions { Limit = 1 });
            if (count > 0)
            {
                throw new PublicOneSubmitDuplicateException();
            }
        }

        public DynamicFilter? BuildReadFilter(PublicFormLinkSetting setting, string? wxOpenId, string ip)
        {
            if (!setting.ViewOwnData && !setting.EditOwnData)
            {
                return null;
            }

            var (field, value) = ResolveDedupKey(setting, wxOpenId, ip, string.Empty, string.Empty);
            return new DynamicFilter
            {
                Field = field,
                Op = FilterOp.Eq,
                Value = value,
            };
        }

        private static (string field, string value) ResolveDedupKey(
            PublicFormLinkSetting setting, string? wxOpenId, string ip, string corpId, string formId)
        {
            if (!string.IsNullOrWhiteSpace(wxOpenId))
            {
                return (WxOpenIdFieldPath, wxOpenId);
            }

            var identity = $"public:{ip}:{corpId}:{formId}";
            return (CreateByIpFieldPath, identity);
        }

        private static DynamicFilter BuildDupFilter(string corpId, string formId, string field, string value)
        {
            return new DynamicFilter
            {
                Rel = FilterRel.And,
                Items =
                [
                    new DynamicFilter { Field = Fields.CorpId, Op = FilterOp.Eq, Value = corpId },
                    new DynamicFilter { Field = Fields.FormId, Op = FilterOp.Eq, Value = formId },
                    new DynamicFilter { Field = field, Op = FilterOp.Eq, Value = value },
                    new DynamicFilter { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true },
                    new DynamicFilter { Field = Fields.CreateTime, Op = FilterOp.Gte, Value = DateTime.UtcNow.Subtract(OneSubmitWindow).ToTimeStampMs() }
                ]
            };
        }
    }

    public sealed class PublicOneSubmitDuplicateException : Exception
    {
        public PublicOneSubmitDuplicateException() : base("已提交过该表单") { }
    }
}
