using HKH.Mef2.Integration;
using EIMSNext.Entities;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
    public class UserService(IResolver resolver) : MongoEntityServiceBase<User>(resolver), IUserService
    {
    }
}
