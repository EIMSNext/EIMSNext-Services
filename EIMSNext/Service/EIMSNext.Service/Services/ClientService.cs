using HKH.Mef2.Integration;
using EIMSNext.Entities;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;

namespace EIMSNext.Service
{
    public class ClientService(IResolver resolver) : MongoEntityServiceBase<EIMSNext.Entities. Client>(resolver), IClientService
    {
    }
}
