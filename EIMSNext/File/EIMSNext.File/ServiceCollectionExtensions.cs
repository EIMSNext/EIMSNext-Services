using EIMSNext.File.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.File
{
    public static class ServiceCollectionExtensions
    {
        public static void AddUploadedServices(this IServiceCollection services)
        {
            services.AddScoped<IUploadedFileService, UploadedFileService>();
        }
    }
}
