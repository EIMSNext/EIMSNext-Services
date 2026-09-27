using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 公开访问校验器，根据当前公开身份与发布设置判断是否可访问仪表盘、仪表盘项、表单定义及表单数据，并据此过滤表单数据查询范围。
    /// </summary>
    /// <param name="resolver">服务解析器。</param>
    public sealed class PublicAccessValidator(IResolver resolver) : ApiServiceBase(resolver), IPublicAccessValidator
    {
        private PublicSetting? _setting;
        private DashboardDef? _dashboard;
        private FormDef? _ownerForm;
        private HashSet<string>? _relatedFormIds;
        private HashSet<string>? _dashboardFormIds;
        private HashSet<string>? _dashboardItemIds;

        /// <summary>
        /// 获取一个值，指示当前身份是否为公开访问身份。
        /// </summary>
        public bool IsPublicIdentity => IdentityContext.IdentityType == IdentityType.Public;

        /// <summary>
        /// 获取当前公开访问目标的 ID（仪表盘 ID）。
        /// </summary>
        public string TargetId => IdentityContext.CurrentDashboardId;

        /// <summary>
        /// 获取当前公开访问目标对应的发布设置；非公开身份或目标为空时返回 null。
        /// </summary>
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

        /// <summary>
        /// 获取一个值，指示当前公开访问目标是否存在任何已启用的发布区块。
        /// </summary>
        public bool IsAnySectionEnabled()
        {
            return GetCurrentSetting() != null;
        }

        /// <summary>
        /// 判断是否可访问指定的公开仪表盘。
        /// </summary>
        /// <param name="dashboardId">仪表盘 ID。</param>
        public bool CanReadDashboard(string dashboardId)
        {
            var setting = GetCurrentSetting();
            return setting?.TargetType == PublicTargetType.Dashboard &&
                   IsSectionAvailable(setting.Dashboard) &&
                   string.Equals(setting.TargetId, dashboardId, StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断是否可访问指定的仪表盘项。
        /// </summary>
        /// <param name="itemId">仪表盘项 ID。</param>
        public bool CanReadDashboardItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !IsDashboardAvailable())
            {
                return false;
            }

            EnsureDashboardScopes();
            return _dashboardItemIds?.Contains(itemId) == true;
        }

        /// <summary>
        /// 判断是否可查看指定表单的定义。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
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

        /// <summary>
        /// 判断是否可向指定表单提交数据。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        public bool CanSubmitForm(string formId)
        {
            var setting = GetCurrentSetting();
            return setting?.TargetType == PublicTargetType.Form &&
                   string.Equals(setting.TargetId, formId, StringComparison.Ordinal) &&
                   IsSectionAvailable(setting.Form.FormLink);
        }

        /// <summary>
        /// 判断是否可查看指定表单的数据。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
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

        /// <summary>
        /// 判断是否可查询指定表单的数据。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
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

                return (IdentityContext.PublicScope == PublicScope.FormLink &&
                        IsSectionAvailable(setting.Form.FormLink) ||
                        IdentityContext.PublicScope == PublicScope.QueryLink &&
                        IsSectionAvailable(setting.Form.QueryLink)) &&
                       IsRelatedForm(formId);
            }

            return CanReadDashboardForm(formId);
        }

        /// <summary>
        /// 判断指定表单是否为当前公开表单的关联表单。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
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

        /// <summary>
        /// 判断是否可访问当前公开仪表盘内引用的指定表单。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        public bool CanReadDashboardForm(string formId)
        {
            if (string.IsNullOrWhiteSpace(formId) || !IsDashboardAvailable())
            {
                return false;
            }

            EnsureDashboardScopes();
            return _dashboardFormIds?.Contains(formId) == true;
        }

        /// <summary>
        /// 获取当前公开访问目标下可读的表单 ID 集合。
        /// </summary>
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

        /// <summary>
        /// 在指定表单数据查询上叠加公开访问的数据范围过滤；无权访问时返回恒不匹配的过滤条件。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        /// <param name="filter">原始过滤条件。</param>
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

        /// <summary>
        /// 判断指定发布区块当前是否可用（已启用且未过期）。
        /// </summary>
        /// <param name="section">发布区块配置。</param>
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
