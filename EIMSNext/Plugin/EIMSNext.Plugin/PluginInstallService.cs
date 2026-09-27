using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Plugin
{
    public class PluginInstallService(IResolver resolver) : MongoEntityServiceBase<PluginInstall>(resolver), IPluginInstallService
    {
    }
}
