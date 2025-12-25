using EIMSNext.Core.Entity;

namespace EIMSNext.ApiService
{
    public interface IIdentityContext
    {
        string CurrentUserID { get; }
        IUser? CurrentUser { get; }
        IEmployee? CurrentEmployee { get; }

        IdentityType IdentityType { get; }
        //AccessControlLevel AccessControlLevel { get; set; }

        string CurrentCorpId { get; }
        string? CurrentAppId { get; }
        string? CurrentFormId { get; }

        string AccessToken { get; }
    }
}
