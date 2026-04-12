using EIMSNext.Common.Extensions;
using EIMSNext.Core.Entities;

namespace EIMSNext.Core.Tests
{
    public class FormData : DynamicEntity
    {
        public FormData()
        {
            CreateTime = DateTime.UtcNow.ToTimeStampMs();
            UpdateTime = CreateTime;
        }

        public FormData(
            string jsonData)
            : base(jsonData)
        {
            CreateTime = DateTime.UtcNow.ToTimeStampMs();
            UpdateTime = CreateTime;
        }

        public string AppId { get; set; } = "";
        public string FormId { get; set; } = "";
        public FlowStatus FlowStatus { get; set; }
    }
}
