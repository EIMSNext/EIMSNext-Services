namespace EIMSNext.Service.Host.Requests
{
    /// <summary>
    /// 
    /// </summary>
    public class DeleteBatch
    {
        /// <summary>
        ///
        /// </summary>
        public List<string>? Keys { get; set; }

        /// <summary>
        /// 表单ID：删除范围必须与数据行的 FormId 一致，不一致的行会被条件排除。
        /// </summary>
        public string? FormId { get; set; }
    }
}

