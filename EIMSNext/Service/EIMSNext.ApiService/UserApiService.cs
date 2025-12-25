using HKH.Mef2.Integration;

using EIMSNext.Auth.Entity;
using EIMSNext.Service.Interface;

namespace EIMSNext.ApiService
{
    public class UserApiService(IResolver resolver) : ApiServiceBase<User, User, IUserService>(resolver)
    {
    }
}
