using System.Composition;

using EIMSNext.Plugin.Contracts;

using HKH.Mef2.Integration;

namespace EIMSNext.Plugin.Internal
{
    /// <summary>
    /// internal plugin can access the IOC object
    /// </summary>
    /// <typeparam name="TSetting"></typeparam>
    public abstract class InternalPluginBase<TSetting> : PluginBase<TSetting>, IPlugin
        where TSetting : class, new()
    {
        [Import(Constants.CONTRCTNAME_RESOLVERWRAPPER)]
        public IResolver? Resolver { get; set; }
    }
}
