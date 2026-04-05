using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Api.Requests
{
    public class PrintRequest
    {
        public string? TemplateId { get; set; }
        public List<string>? DataIds { get; set; }
    }
    public class PrintPreviewRequest
    {
        public string Content { get; set; } = string.Empty;
        public PrintTemplateType PrintType { get; set; } = PrintTemplateType.Pdf;
    }
}
