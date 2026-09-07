namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 申请加入企业请求。
    /// </summary>
    public class ApplyJoinCorporateRequest
    {
        /// <summary>企业 ID。</summary>
        public string CorpId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 审核加入企业请求。
    /// </summary>
    public class ReviewJoinCorporateRequest
    {
        /// <summary>员工 ID 列表。</summary>
        public List<string>? EmployeeIds { get; set; }

        /// <summary>是否批准。</summary>
        public bool Approved { get; set; }
    }

    /// <summary>
    /// 接受员工邀请请求。
    /// </summary>
    public class AcceptEmployeeInviteRequest
    {
        /// <summary>是否接受。</summary>
        public bool Accepted { get; set; }
    }
}
