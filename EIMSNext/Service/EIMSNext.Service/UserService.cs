using HKH.Mef2.Integration;
using EIMSNext.Auth.Entity;
using EIMSNext.Core.Service;
using EIMSNext.Service.Interface;

namespace EIMSNext.Service
{
    public class UserService(IResolver resolver) : MongoEntityServiceBase<User>(resolver), IUserService
    {
    }
}
