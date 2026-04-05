namespace EIMSNext.Service.Api.Requests
{
    public class PrintRequest
    {
        public string? TemplateId { get; set; }
        public List<string>? DataIds { get; set; }
    }
}
