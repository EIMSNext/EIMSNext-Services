using EIMSNext.ApiService;
using EIMSNext.Core.Entity;

namespace EIMSNext.ApiHost.Authorization
{
    public interface IIdentityContext
    {
        string CurrentUserID { get; }
        IEmployee CurrentEmployee { get; }

        string CurrentCorpId { get; }
        string? CurrentAppId { get; }
        string? CurrentFormId { get; }


        IUser? CurrentUser { get; }

        IdentityType IdentityType { get; }
        //AccessControlLevel AccessControlLevel { get; set; }

        string AccessToken { get; }
    }
}
