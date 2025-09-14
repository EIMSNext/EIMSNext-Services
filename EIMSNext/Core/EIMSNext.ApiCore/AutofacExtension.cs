using System.Composition.Convention;

using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using HKH.Mef2.Integration;

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="services"></param>
        /// <param name="dir"></param>
        /// <param name="searchPattern"></param>
        /// <param name="independentLoad">是否独立加载</param>
        public static void AddDefaultMef(this IServiceCollection services, string dir, string searchPattern = "*.dll")
        {
            var mefContainer = new DefaultContainerConfiguration();

            // 创建约定构建器, 默认为Shared
            var sharedConventions = new ConventionBuilder();
            sharedConventions.ForTypesMatching(_ => true).Shared();

            mefContainer.WithAssemblies(dir, searchPattern, SearchOption.AllDirectories, sharedConventions);
            services.EnableMef2(mefContainer);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="services"></param>
        /// <param name="files"></param>
        public static void AddReloadableMef(this IServiceCollection services, IEnumerable<string> files)
        {
            var mefContainer = new ReloadableContainerConfiguration();
            // 创建约定构建器, 默认为Shared
            var sharedConventions = new ConventionBuilder();
            sharedConventions.ForTypesMatching(_ => true).Shared();

            mefContainer.Reload(files, sharedConventions);

            services.EnableMef2(mefContainer);
        }
    }
}
