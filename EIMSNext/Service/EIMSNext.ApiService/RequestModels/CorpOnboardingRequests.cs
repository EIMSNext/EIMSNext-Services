namespace EIMSNext.ApiService.RequestModels
{
    public class ApplyJoinCorporateRequest
    {
        public string CorpId { get; set; } = string.Empty;
    }

    public class ReviewJoinCorporateRequest
    {
        public List<string>? EmployeeIds { get; set; }

        public bool Approved { get; set; }
    }

    public class AcceptEmployeeInviteRequest
    {
        public bool Accepted { get; set; }
    }
}
