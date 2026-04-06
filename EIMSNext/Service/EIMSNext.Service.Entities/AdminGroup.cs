using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 管理组
    /// </summary>
    public class AdminGroup : CorpEntityBase
    {
        /// <summary>
        /// 管理组名称
        /// </summary>
        public string Name { get; set; }=string.Empty;
    }
}
