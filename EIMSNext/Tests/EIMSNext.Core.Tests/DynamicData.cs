using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Core.Tests
{
    public class FormData : DynamicEntity
    {
        public FormData()
        {
            CreateTime = DateTime.UtcNow.ToTimeStampMs();
        }

        public FormData(
            string jsonData)
            : base(jsonData)
        {
            CreateTime = DateTime.UtcNow.ToTimeStampMs();
        }

        public string AppId { get; set; } = "";
        public string FormId { get; set; } = "";
        public FlowStatus FlowStatus { get; set; }
    }
}
