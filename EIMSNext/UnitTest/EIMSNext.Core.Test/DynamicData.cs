using EIMSNext.Common.Extension;
using EIMSNext.Core.Entity;

namespace EIMSNext.Core.Test
{
    public class DynamicData : DynamicEntity
    {
        public DynamicData()
        {
            CreateTime = DateTime.UtcNow.ToTimeStampMs();
            UpdateTime = CreateTime;
        }

        public DynamicData(
            string jsonData)
            : base(jsonData)
        {
            CreateTime = DateTime.UtcNow.ToTimeStampMs();
            UpdateTime = CreateTime;
        }

        public string AppId { get; set; } = "";
        public string FormId { get; set; } = "";
    }

    public class FormData : DynamicEntity
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 流程状态
        /// </summary>
        public FlowStatus FlowStatus { get; set; }
    }
}
