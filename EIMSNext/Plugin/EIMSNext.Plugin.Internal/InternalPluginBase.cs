using EIMSNext.Plugin.Contracts;

namespace EIMSNext.Plugin.Internal
{
    /// <summary>
    /// internal plugin can access the IOC object
    /// </summary>
    /// <typeparam name="TSetting"></typeparam>
    public abstract class InternalPluginBase<TSetting> : PluginBase<TSetting>, IPlugin
        where TSetting : class, new()
    {
        protected TResolver GetResolver<TResolver>() where TResolver : class
        {
            if (Context?.Resolver is not TResolver resolver)
            {
                throw new InvalidOperationException($"Resolver [{typeof(TResolver).FullName}] is not available.");
            }

            return resolver;
        }
    }
}
