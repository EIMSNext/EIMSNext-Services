using System.Text.Json;
using System.Text.Json.Serialization;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 工作流待办事项实体
    /// </summary>
    public class Wf_Task : CorpEntityBase
    {
        /// <summary>
        /// 表单名称快照。
        /// </summary>
        public string? FormName { get; set; }
        /// <summary>
        /// 工作流实例ID
        /// </summary>
        public string WfInstanceId { get; set; } = string.Empty;
        /// <summary>
        /// 审批节点ID
        /// </summary>
        public string ApproveNodeId { get; set; } = string.Empty;
        /// <summary>
        /// 审批节点名称
        /// </summary>
        public string ApproveNodeName { get; set; } = string.Empty;
        /// <summary>
        /// 待办员工ID
        /// </summary>
        public string EmployeeId { get; set; } = string.Empty;
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 表单ID
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 业务数据ID
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 表单类型
        /// </summary>
        public int FormType { get; set; } = 0;
        /// <summary>
        /// 流程发起人
        /// </summary>
        public Operator? Starter { get; set; }
        /// <summary>
        /// 审批节点开始时间（时间戳）
        /// </summary>
        public long ApproveNodeStartTime { get; set; }
        /// <summary>
        /// 数据摘要字段列表
        /// </summary>
        public List<BriefField> DataBrief { get; set; } = new List<BriefField>();
        /// <summary>
        /// 超时时间
        /// </summary>
        public long? ExpireTime { get; set; }
        /// <summary>
        /// 超时动作是否已处理
        /// </summary>
        public bool ExpireHandled { get; set; }
    }

    /// <summary>
    /// 摘要字段，用于展示待办数据的关键信息
    /// </summary>
    public class BriefField
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string Field { get; set; } = string.Empty;
        /// <summary>
        /// 字段显示标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 字段值（规整后的字符串）
        /// </summary>
        [JsonConverter(typeof(BriefValueConverter))]
        public string? Value { get; set; }

        /// <summary>
        /// 从表单字段定义与动态数据生成数据摘要。
        /// </summary>
        /// <remarks>
        /// dataselect 不进摘要：值为 {appId,formId,dataId,data} 对象快照，卡片展示冗长。
        /// </remarks>
        public static List<BriefField> FromData(IEnumerable<FieldDef>? items, Dictionary<string, object?>? data)
        {
            var brief = new List<BriefField>();
            if (data is null) return brief;

            foreach (var field in (items ?? []).Where(x => x.Type != FieldType.DataSelect).Take(6))
            {
                brief.Add(new BriefField { Field = field.Field, Title = field.Title, Value = ToBriefValue(field, data.GetValueOrDefault(field.Field)) });
            }
            return brief;
        }

        /// <summary>
        /// 把 FormData.Data 的动态值规整为摘要字符串。
        /// </summary>
        /// <remarks>
        /// 不能保留 object：OData 对 object 声明成员会写成
        /// <c>{ "value@odata.type": "#String", "value": "..." }</c> 包装，
        /// 前端（裸 HTTP+JSON）与通知模板都拿不到裸值。
        /// employee1/employee2/department1/department2 取 label；其余复合形状退回 JSON 序列化（保持旧行为）。
        /// </remarks>
        public static string? ToBriefValue(FieldDef? field, object? value)
        {
            if (value is null) return null;
            if (value is string s) return s;
            if (value is Dictionary<string, object?> dict)
            {
                if (dict.TryGetValue("label", out var label) && label is string text && !string.IsNullOrWhiteSpace(text))
                    return text;
                return JsonSerializer.Serialize(value);
            }
            if (value is List<object?> list)
            {
                var parts = list
                    .Select(item => ToBriefValue(field, item))
                    .Where(item => !string.IsNullOrEmpty(item))
                    .ToList();
                return parts.Count > 0 ? string.Join("、", parts) : JsonSerializer.Serialize(value);
            }
            return JsonSerializer.Serialize(value);
        }
    }

    /// <summary>
    /// <see cref="BriefField.Value"/> 的容错转换：模型收窄为 string 前落库的 jsonb 里
    /// 可能是 JSON number/bool/对象，直接反序列化会抛
    /// <c>Cannot get the value of a token type 'Number' as a string</c>（OData 查询 500）。
    /// 统一按文本还原，与旧 object 模型下 <c>Value.ToString()</c> 的表现一致。
    /// </summary>
    public class BriefValueConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var element = document.RootElement;
            return element.ValueKind == JsonValueKind.Null ? null : element.ToString();
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value);
        }
    }
}
