using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Host.Requests
{
    public class PrintRequest
    {
        public string? PrintId { get; set; }
        public List<string>? DataIds { get; set; }
    }
    public class PrintPreviewRequest
    {
        public string Content { get; set; } = string.Empty;
        public PrintDefType PrintType { get; set; } = PrintDefType.Pdf;
    }
}
