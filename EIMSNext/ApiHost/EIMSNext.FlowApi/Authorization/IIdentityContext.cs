using EIMSNext.Core.Entity;

namespace EIMSNext.FlowApi.Authorization
{
    public interface IIdentityContext
    {
        string CurrentUserID { get; }
        IEmployee CurrentEmployee { get; }

        string CurrentCorpId { get; }
        string? CurrentAppId { get; }
        string? CurrentFormId { get; }
    }
}
