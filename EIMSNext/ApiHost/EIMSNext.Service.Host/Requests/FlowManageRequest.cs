namespace EIMSNext.Service.Host.Requests
{
    public class FlowManageQueryRequest
    {
        public string Keyword { get; set; } = string.Empty;
        public int PageNum { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
