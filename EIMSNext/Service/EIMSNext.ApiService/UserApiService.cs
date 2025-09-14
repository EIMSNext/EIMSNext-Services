using HKH.Mef2.Integration;

using EIMSNext.Auth.Entity;

namespace EIMSNext.ApiService
{
    public class UserApiService(IResolver resolver) : ApiServiceBase<User, User>(resolver)
    {
    }
}
