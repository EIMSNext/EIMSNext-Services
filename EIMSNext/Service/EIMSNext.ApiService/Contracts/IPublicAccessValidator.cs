using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService
{
    public interface IPublicAccessValidator
    {
        bool IsPublicIdentity { get; }

        string TargetId { get; }

        PublicSetting? GetCurrentSetting();

        bool IsAnySectionEnabled();

        bool CanReadDashboard(string dashboardId);

        bool CanReadDashboardItem(string itemId);

        bool CanReadFormDefinition(string formId);

        bool CanSubmitForm(string formId);

        bool CanReadFormData(string formId);

        bool CanQueryFormData(string formId);

        bool CanReadDashboardForm(string formId);

        IReadOnlyCollection<string> GetReadableFormIds();

        DynamicFilter ApplyFormDataScope(string formId, DynamicFilter? filter);
    }
}
