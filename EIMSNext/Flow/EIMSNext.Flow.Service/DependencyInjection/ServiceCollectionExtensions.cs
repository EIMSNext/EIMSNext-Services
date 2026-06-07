using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Service.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Flow.Service
{
    public static class ServiceCollectionExtensions
    {
        public static void AddWorkflowServices(this IServiceCollection services)
        {
            services.AddScoped<IWorkflowLoader, WorkflowLoader>();
            services.AddTransient<IDfDataProcessor, DfDataProcessor>();
            services.AddScoped<IDataflowHookService, DataflowHookService>();
        }
    }
}
