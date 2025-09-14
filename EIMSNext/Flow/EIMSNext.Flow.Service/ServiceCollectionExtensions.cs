using EIMSNext.Flow.Core.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Flow.Service
{
    public static class ServiceCollectionExtensions
    {
        public static void AddWorkflowServices(this IServiceCollection services)
        {
            services.AddScoped<IWorkflowLoader, WorkflowLoader>();
            services.AddTransient<IDfDataProcessor, DfDataProcessor>();
        }
    }
}
