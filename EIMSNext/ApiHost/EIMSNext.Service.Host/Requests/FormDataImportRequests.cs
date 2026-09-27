using Microsoft.AspNetCore.Http;

namespace EIMSNext.Service.Host.Requests
{
    public class FormDataImportPreviewRequest
    {
        public IFormFile? File { get; set; }

        public string? FormId { get; set; }
    }

    public class FormDataImportRequest
    {
        public IFormFile? File { get; set; }

        public string? Options { get; set; }
    }
}
