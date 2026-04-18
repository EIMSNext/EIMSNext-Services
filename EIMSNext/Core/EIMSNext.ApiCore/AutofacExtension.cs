using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EIMSNext.ApiCore
{
    /// <summary>
    /// 
    /// </summary>
    public static class AutofacExtension
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hostBuilder"></param>
        public static void UseAutofac<T>(this IHostBuilder hostBuilder) where T : IModule, new()
        {
            hostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory());
            hostBuilder.ConfigureContainer<ContainerBuilder>(ContainerBuilder => ContainerBuilder.RegisterModule(new T()));
        }

    }
}
