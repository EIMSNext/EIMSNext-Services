namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 表单数据管理请求。
    /// </summary>
    public class FormDataManageRequest
    {
        /// <summary>数据主键 ID 列表。</summary>
        public List<string>? Keys { get; set; }
    }
}
