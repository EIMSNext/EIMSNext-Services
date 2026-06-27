using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class CrossBindingService(IResolver resolver) : EntityServiceBase<CrossBinding>(resolver), ICrossBindingService
    {
    }
}
