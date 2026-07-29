using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    public class FormDataFilterOptionsRequest
    {
        public string FormId { get; set; } = string.Empty;

        public string Field { get; set; } = string.Empty;

        public string? FieldType { get; set; }

        public string? Keyword { get; set; }

        public DynamicFilter? Filter { get; set; }

        public string? AuthGroupId { get; set; }

        public int Limit { get; set; } = 50;
    }

    public class FormDataFilterOptionsResponse
    {
        public List<FilterOptionItem> Items { get; set; } = [];
    }

    public class FormDataPermissionScopeResponse
    {
        public DataPerms DataPerms { get; set; }

        public List<FieldPerm>? FieldPerms { get; set; }
    }
}
