namespace EIMSNext.ApiService.RequestModels
{
    public class WorkbenchConfigRequest : RequestBase
    {
        public string Layout { get; set; } = string.Empty;

        public string PageStyle { get; set; } = string.Empty;
    }

    public class WorkbenchFavoriteRequest : RequestBase
    {
        public string TargetType { get; set; } = string.Empty;

        public string TargetId { get; set; } = string.Empty;

        public long SortIndex { get; set; }
    }

    public class WorkbenchRecentVisitRequest : RequestBase
    {
        public string TargetType { get; set; } = string.Empty;

        public string TargetId { get; set; } = string.Empty;
    }
}
