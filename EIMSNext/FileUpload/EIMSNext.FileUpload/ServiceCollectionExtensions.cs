using EIMSNext.Service;
using EIMSNext.Service.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.FileUpload
{
    public static class ServiceCollectionExtensions
    {
        public static void AddUploadedServices(this IServiceCollection services)
        {
            services.AddScoped<IUploadedFileService, UploadedFileService>();
        }
    }
}
