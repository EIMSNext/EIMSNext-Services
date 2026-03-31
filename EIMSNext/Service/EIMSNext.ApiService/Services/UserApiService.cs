using HKH.Mef2.Integration;

using EIMSNext.Auth.Entities;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
    public class UserApiService(IResolver resolver) : ApiServiceBase<User, User, IUserService>(resolver)
    {
    }
}
