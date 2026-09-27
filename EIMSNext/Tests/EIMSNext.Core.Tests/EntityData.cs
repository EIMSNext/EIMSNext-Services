using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;

namespace EIMSNext.Core.Tests
{
    public class EntityData : EntityBase
    {
        public EntityData()
        {
            CreateTime = DateTime.UtcNow.ToTimeStampMs();
            UpdateTime = CreateTime;
        }

        public string AppId { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public FieldDefList Fields { get; set; } = new FieldDefList();
    }

    public class FieldDefList : List<FieldDef> { }
    public class FieldDef
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; }= string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
