namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 表单数据管理请求。
    /// </summary>
    public class FormDataManageRequest
    {
        /// <summary>数据主键 ID 列表。</summary>
        public List<string>? Keys { get; set; }

        /// <summary>表单ID：删除/恢复范围必须与数据行的 FormId 一致，不一致的行会被条件排除。</summary>
        public string? FormId { get; set; }
    }
}
