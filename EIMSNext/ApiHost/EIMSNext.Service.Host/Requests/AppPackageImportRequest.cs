using Microsoft.AspNetCore.Http;

namespace EIMSNext.Service.Host.Requests
{
    public class AppPackageImportRequest
    {
        public IFormFile? File { get; set; }
    }
}
