using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.ApiService
{
    public interface IIdentityContext
    {
        string CurrentUserID { get; }
        IUser? CurrentUser { get; }
        IEmployee? CurrentEmployee { get; }

        IdentityType IdentityType { get; }
        AccessControlLevel AccessControlLevel { get; set; }

        string CurrentCorpId { get; }

        string CurrentDashboardId { get; }

        string AccessToken { get; }

        PublicScope PublicScope { get; }
    }
}
