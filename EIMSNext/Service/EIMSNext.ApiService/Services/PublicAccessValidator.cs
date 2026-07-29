using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public sealed class PublicAccessValidator(IResolver resolver) : ApiServiceBase(resolver), IPublicAccessValidator
    {
        private PublicSetting? _setting;
        private DashboardDef? _dashboard;
        private FormDef? _ownerForm;
        private HashSet<string>? _relatedFormIds;
        private HashSet<string>? _dashboardFormIds;
        private HashSet<string>? _dashboardItemIds;

        public bool IsPublicIdentity => IdentityContext.IdentityType == IdentityType.Public;

        public string TargetId => IdentityContext.CurrentDashboardId;

        public PublicSetting? GetCurrentSetting()
        {
            if (!IsPublicIdentity || string.IsNullOrWhiteSpace(TargetId))
            {
                return null;
            }

            return _setting ??= Resolver.Resolve<IPublicSettingService>()
                .Query(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.TargetId == TargetId)
                .ToList()
                .FirstOrDefault(IsAnyPublishEnabled);
        }

        public bool IsAnySectionEnabled()
        {
            return GetCurrentSetting() != null;
        }

        public bool CanReadDashboard(string dashboardId)
        {
            var setting = GetCurrentSetting();
            return setting?.TargetType == PublicTargetType.Dashboard &&
                   IsSectionAvailable(setting.Dashboard) &&
                   string.Equals(setting.TargetId, dashboardId, StringComparison.Ordinal);
        }

        public bool CanReadDashboardItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !IsDashboardAvailable())
            {
                return false;
            }

            EnsureDashboardScopes();
            return _dashboardItemIds?.Contains(itemId) == true;
        }

        public bool CanReadFormDefinition(string formId)
        {
            var setting = GetCurrentSetting();
            if (setting == null)
            {
                return false;
            }

            if (setting.TargetType == PublicTargetType.Form)
            {
                return string.Equals(setting.TargetId, formId, StringComparison.Ordinal) && IsAnyFormPublishAvailable();
            }

            return CanReadDashboardForm(formId);
        }

        public bool CanSubmitForm(string formId)
        {
            var setting = GetCurrentSetting();
            return setting?.TargetType == PublicTargetType.Form &&
                   string.Equals(setting.TargetId, formId, StringComparison.Ordinal) &&
                   IsSectionAvailable(setting.Form.FormLink);
        }

        public bool CanReadFormData(string formId)
        {
            var setting = GetCurrentSetting();
            if (setting == null)
            {
                return false;
            }

            if (setting.TargetType == PublicTargetType.Form)
            {
                return string.Equals(setting.TargetId, formId, StringComparison.Ordinal) &&
                       IsSectionAvailable(setting.Form.DataLink);
            }

            return CanReadDashboardForm(formId);
        }

        public bool CanQueryFormData(string formId)
        {
            var setting = GetCurrentSetting();
            if (setting == null)
            {
                return false;
            }

            if (setting.TargetType == PublicTargetType.Form)
            {
                if (string.Equals(setting.TargetId, formId, StringComparison.Ordinal))
                {
                    return IsSectionAvailable(setting.Form.QueryLink);
                }

                return IdentityContext.PublicScope == PublicScope.FormLink &&
                       IsSectionAvailable(setting.Form.FormLink) &&
                       IsRelatedForm(formId);
            }

            return CanReadDashboardForm(formId);
        }

        public bool IsRelatedForm(string formId)
        {
            if (string.IsNullOrWhiteSpace(formId))
            {
                return false;
            }

            var setting = GetCurrentSetting();
            if (setting?.TargetType != PublicTargetType.Form)
            {
                return false;
            }

            EnsureRelatedFormIds();
            return _relatedFormIds?.Contains(formId) == true;
        }

        public bool CanReadDashboardForm(string formId)
        {
            if (string.IsNullOrWhiteSpace(formId) || !IsDashboardAvailable())
            {
                return false;
            }

            EnsureDashboardScopes();
            return _dashboardFormIds?.Contains(formId) == true;
        }

        public IReadOnlyCollection<string> GetReadableFormIds()
        {
            var setting = GetCurrentSetting();
            if (setting == null)
            {
                return [];
            }

            if (setting.TargetType == PublicTargetType.Form && IsAnyFormPublishAvailable())
            {
                return string.IsNullOrWhiteSpace(setting.TargetId) ? [] : [setting.TargetId];
            }

            if (setting.TargetType == PublicTargetType.Dashboard && IsDashboardAvailable())
            {
                EnsureDashboardScopes();
                return _dashboardFormIds?.ToList() ?? [];
            }

            return [];
        }

        public DynamicFilter ApplyFormDataScope(string formId, DynamicFilter? filter)
        {
            if (!CanReadFormData(formId) && !CanQueryFormData(formId) && !CanReadDashboardForm(formId))
            {
                return CreateNoMatchFilter();
            }

            var setting = GetCurrentSetting();
            if (setting == null)
            {
                return CreateNoMatchFilter();
            }

            var scopeFilter = new DynamicFilter
            {
                Rel = FilterRel.And,
                Items =
                [
                    new DynamicFilter { Field = Fields.CorpId, Op = FilterOp.Eq, Value = setting.CorpId ?? string.Empty },
                    new DynamicFilter { Field = Fields.FormId, Op = FilterOp.Eq, Value = formId },
                    new DynamicFilter { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true },
                ],
            };

            return filter == null || filter.IsEmpty
                ? scopeFilter
                : new DynamicFilter { Rel = FilterRel.And, Items = [scopeFilter, filter] };
        }

        private bool IsDashboardAvailable()
        {
            var setting = GetCurrentSetting();
            if (setting?.TargetType != PublicTargetType.Dashboard || !IsSectionAvailable(setting.Dashboard))
            {
                return false;
            }

            var dashboard = GetCurrentDashboard();
            return dashboard != null && !dashboard.DeleteFlag;
        }

        private bool IsAnyFormPublishAvailable()
        {
            var setting = GetCurrentSetting();
            return setting?.TargetType == PublicTargetType.Form &&
                   (IsSectionAvailable(setting.Form.FormLink) ||
                    IsSectionAvailable(setting.Form.DataLink) ||
                    IsSectionAvailable(setting.Form.QueryLink));
        }

        private DashboardDef? GetCurrentDashboard()
        {
            var setting = GetCurrentSetting();
            if (setting?.TargetType != PublicTargetType.Dashboard)
            {
                return null;
            }

            return _dashboard ??= Resolver.Resolve<IDashboardDefService>().Get(setting.TargetId);
        }

        private void EnsureRelatedFormIds()
        {
            if (_relatedFormIds != null)
            {
                return;
            }

            _relatedFormIds = [];
            var setting = GetCurrentSetting();
            if (setting?.TargetType != PublicTargetType.Form)
            {
                return;
            }

            _ownerForm ??= Resolver.Resolve<IFormDefService>().Get(setting.TargetId);
            if (_ownerForm == null || _ownerForm.DeleteFlag ||
                !string.Equals(_ownerForm.CorpId, setting.CorpId, StringComparison.Ordinal))
            {
                return;
            }

            var relatedIds = _ownerForm.PublicRelatedFormIds.Count > 0
                ? _ownerForm.PublicRelatedFormIds
                : FormRelatedSourceResolver.ResolveFormIds(_ownerForm.Content.Layout);
            _relatedFormIds.UnionWith(relatedIds);
        }

        private void EnsureDashboardScopes()
        {
            if (_dashboardFormIds != null && _dashboardItemIds != null)
            {
                return;
            }

            _dashboardFormIds = [];
            _dashboardItemIds = [];

            var dashboard = GetCurrentDashboard();
            if (dashboard == null || !IsDashboardAvailable())
            {
                return;
            }

            var items = Resolver.Resolve<IDashboardItemDefService>()
                .Query(x => x.CorpId == dashboard.CorpId && !x.DeleteFlag && x.DashboardId == dashboard.Id)
                .ToList();

            foreach (var item in items)
            {
                _dashboardItemIds.Add(item.Id);
                var formId = ResolveItemFormId(item);
                if (!string.IsNullOrWhiteSpace(formId))
                {
                    _dashboardFormIds.Add(formId);
                }
            }
        }

        private static string? ResolveItemFormId(DashboardItemDef item)
        {
            if (string.IsNullOrWhiteSpace(item.Details))
            {
                return null;
            }

            try
            {
                var doc = MongoDB.Bson.BsonDocument.Parse(item.Details);
                if (!doc.TryGetValue("datasource", out var datasourceValue) ||
                    !datasourceValue.IsBsonDocument ||
                    !datasourceValue.AsBsonDocument.TryGetValue("id", out var idValue))
                {
                    return null;
                }

                return idValue.IsString ? idValue.AsString : idValue.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsAnyPublishEnabled(PublicSetting setting)
        {
            if (setting.TargetType == PublicTargetType.Dashboard)
            {
                return IsSectionAvailable(setting.Dashboard);
            }

            return IsSectionAvailable(setting.Form.FormLink) ||
                   IsSectionAvailable(setting.Form.DataLink) ||
                   IsSectionAvailable(setting.Form.QueryLink);
        }

        public static bool IsSectionAvailable(PublicPublishSection? section)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return section?.Enabled == true && (!section.ExpireTime.HasValue || section.ExpireTime.Value > now);
        }

        private static DynamicFilter CreateNoMatchFilter()
        {
            return new DynamicFilter
            {
                Field = Fields.BsonId,
                Op = FilterOp.Eq,
                Value = "__no_permission__",
            };
        }
    }
}
