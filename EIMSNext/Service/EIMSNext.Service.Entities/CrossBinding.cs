using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 跨应用表单绑定关系。
    /// </summary>
    public class CrossBinding : CorpEntityBase
    {
        /// <summary>
        /// 可访问外部表单的目标应用ID。
        /// </summary>
        public string TargetAppId { get; set; } = string.Empty;

        /// <summary>
        /// 外部表单所属应用ID。
        /// </summary>
        public string SourceAppId { get; set; } = string.Empty;

        /// <summary>
        /// 外部表单ID。
        /// </summary>
        public string SourceFormId { get; set; } = string.Empty;
    }
}
