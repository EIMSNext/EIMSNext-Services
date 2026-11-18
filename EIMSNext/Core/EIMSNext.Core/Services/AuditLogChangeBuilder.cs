using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EIMSNext.Core.Abstractions;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 单条字段变更，写入 AuditLog.Detail 的 JSON 数组元素。
    /// </summary>
    public sealed class AuditLogChange
    {
        /// <summary>字段标识（属性名或表单字段 ID）。</summary>
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        /// <summary>字段控件类型，供前端格式化值。</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>字段中文名。</summary>
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        /// <summary>变更前的值。</summary>
        [JsonPropertyName("old")]
        public object? Old { get; set; }

        /// <summary>变更后的值。</summary>
        [JsonPropertyName("new")]
        public object? New { get; set; }
    }

    /// <summary>
    /// 审计日志变更内容的生成与格式化。Detail 恒为合法 JSON 数组，可读串由前端/导出侧渲染。
    /// </summary>
    public static class AuditLogChangeBuilder
    {
        /// <summary>无逐条变更时的空数组。</summary>
        public const string EmptyChanges = "[]";

        /// <summary>单条日志最多记录的变更字段数。</summary>
        public const int MaxItems = 50;

        /// <summary>单个值/条件的最大字符数。</summary>
        public const int MaxValueLength = 200;

        /// <summary>条件字符串的最大字符数。</summary>
        public const int MaxFilterLength = 2000;

        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };

        private static readonly HashSet<string> SystemFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "id", "createTime", "updateTime", "createBy", "updateBy",
            "deleteFlag", "deleteBy", "deleteTime", "corpId",
        };

        private static readonly string[] SummaryFields =
        {
            "name", "title", "serialNo", "serialno", "code", "label", "formName", "appName", "formId", "appId",
        };

        private static readonly Dictionary<string, string> FieldLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "名称", ["title"] = "标题", ["content"] = "内容", ["data"] = "数据",
            ["formId"] = "表单ID", ["appId"] = "应用ID", ["formName"] = "表单名称", ["appName"] = "应用名称",
            ["serialNo"] = "流水号", ["serialno"] = "流水号", ["code"] = "编码", ["label"] = "标签",
            ["remark"] = "备注", ["description"] = "描述", ["status"] = "状态", ["flowStatus"] = "流程状态",
            ["sortIndex"] = "排序", ["enabled"] = "启用", ["type"] = "类型", ["category"] = "分类",
            ["layout"] = "布局", ["settings"] = "设置", ["formSettings"] = "表单设置",
            ["usingWorkflow"] = "启用流程", ["lastVisitTime"] = "最后访问时间", ["visitCount"] = "访问次数",
            ["fieldChangeLogs"] = "字段变更日志", ["dataId"] = "数据ID",
            ["applicantName"] = "申请人",
        };

        /// <summary>把变更列表序列化为 Detail 用的 JSON 数组。</summary>
        public static string Serialize(IReadOnlyList<AuditLogChange> changes)
            => JsonSerializer.Serialize(changes, Options);

        /// <summary>取字段中文名，未命中映射时返回原字段名。</summary>
        public static string LabelOf(string field)
            => FieldLabels.TryGetValue(field, out var label) ? label : field;

        /// <summary>系统字段不进入变更明细。</summary>
        public static bool IsSystemField(string field) => SystemFields.Contains(field);

        /// <summary>按字段名和值推断控件类型。</summary>
        public static string TypeOf(string field, object? value)
        {
            var lower = field.ToLowerInvariant();
            if (lower.Contains("time") || lower.Contains("date")) return "datetime";

            return ValueKindOf(value) switch
            {
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "switch",
                JsonValueKind.Object or JsonValueKind.Array => "json",
                _ => "input",
            };
        }

        /// <summary>把任意字段值规范化为可写入 old/new 的标量或压缩 JSON 串；field 供毫秒时间戳识别。</summary>
        public static object? Normalize(object? value, string? field = null)
        {
            if (value is null) return null;
            var node = JsonSerializer.SerializeToNode(value, Options);
            return NormalizeNode(node, field);
        }

        /// <summary>截断批量条件，避免超长表达式落库。</summary>
        public static string LimitFilter(string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return string.Empty;
            return Clamp(filter.Trim(), MaxFilterLength);
        }

        /// <summary>
        /// 逐字段差异：按新对象属性顺序输出，跳过系统字段与未变更字段。
        /// </summary>
        public static string BuildChanges<T>(T oldT, T newT) where T : class
        {
            var oldJson = ParseObject(oldT);
            var newJson = ParseObject(newT);
            if (oldJson is null || newJson is null) return EmptyChanges;

            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in newJson)
            {
                if (seen.Add(kv.Key)) keys.Add(kv.Key);
            }
            foreach (var kv in oldJson)
            {
                if (seen.Add(kv.Key)) keys.Add(kv.Key);
            }

            var changes = new List<AuditLogChange>();
            foreach (var key in keys)
            {
                if (IsSystemField(key)) continue;

                oldJson.TryGetPropertyValue(key, out var oldNode);
                newJson.TryGetPropertyValue(key, out var newNode);
                if (JsonNode.DeepEquals(oldNode, newNode)) continue;

                changes.Add(new AuditLogChange
                {
                    Field = key,
                    Type = TypeOfNode(key, newNode ?? oldNode),
                    Label = LabelOf(key),
                    Old = NormalizeNode(oldNode, key),
                    New = NormalizeNode(newNode, key),
                });

                if (changes.Count >= MaxItems) break;
            }

            return Serialize(changes);
        }

        /// <summary>
        /// 新增/删除摘要：只取少量主字段，不写全量对象；无主字段时退回数据ID。
        /// </summary>
        public static string BuildSummary<T>(T entity, bool insert) where T : class, IEntityKey
        {
            var json = ParseObject(entity);
            var changes = new List<AuditLogChange>();

            if (json is not null)
            {
                foreach (var wanted in SummaryFields)
                {
                    var key = json.Select(kv => kv.Key)
                        .FirstOrDefault(k => string.Equals(k, wanted, StringComparison.OrdinalIgnoreCase));
                    if (key is null || SystemFields.Contains(key)) continue;
                    if (!json.TryGetPropertyValue(key, out var node) || node is null) continue;
                    if (node.GetValueKind() is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null) continue;

                    var value = NormalizeNode(node, key);
                    if (value is null || (value is string text && text.Length == 0)) continue;

                    changes.Add(new AuditLogChange
                    {
                        Field = key,
                        Type = TypeOfNode(key, node),
                        Label = LabelOf(key),
                        Old = insert ? null : value,
                        New = insert ? value : null,
                    });

                    if (changes.Count >= 3) break;
                }
            }

            if (changes.Count == 0)
            {
                changes.Add(new AuditLogChange
                {
                    Field = "dataId",
                    Type = "input",
                    Label = LabelOf("dataId"),
                    Old = insert ? null : entity.Id,
                    New = insert ? entity.Id : null,
                });
            }

            return Serialize(changes);
        }

        /// <summary>
        /// 导出/降级用：JSON 数组格式化为 "label: old → new" 串；历史非 JSON 文本原样返回。
        /// 批量 ExecuteUpdate 只记录被设置的字段（old 恒 null），用 action 与 old 全空判定，
        /// 避免把「新增」误显示成「无旧值」，也不依赖 DataFilter 是否存在。
        /// </summary>
        public static string FormatForExport(string? detail, DbAction action = DbAction.None)
        {
            if (string.IsNullOrWhiteSpace(detail)) return "-";
            var changes = Parse(detail);
            if (changes is null) return Clamp(detail, MaxFilterLength);
            if (changes.Count == 0) return "批量操作（无逐字段变更）";

            var batch = action == DbAction.Update && changes.All(x => x.Old is null);
            var text = string.Join("；", changes.Select(x =>
                $"{x.Label}: {(batch ? "—" : FormatScalar(x.Old))} → {FormatScalar(x.New)}"));
            return Clamp(text, MaxFilterLength, changes.Count);
        }

        private static List<AuditLogChange>? Parse(string? detail)
        {
            if (string.IsNullOrWhiteSpace(detail) || !detail.TrimStart().StartsWith('[')) return null;
            try { return JsonSerializer.Deserialize<List<AuditLogChange>>(detail, Options); }
            catch (JsonException) { return null; }
        }

        private static string FormatScalar(object? value)
        {
            return value switch
            {
                null => "(空)",
                string s => s,
                JsonElement e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : e.ToString(),
                _ => value.ToString() ?? string.Empty,
            };
        }

        private static JsonObject? ParseObject<T>(T value) where T : class
        {
            try { return JsonNode.Parse(value.SerializeToJson()) as JsonObject; }
            catch (JsonException) { return null; }
        }

        private static JsonValueKind ValueKindOf(object? value)
        {
            if (value is null) return JsonValueKind.Null;
            try { return JsonSerializer.SerializeToNode(value, Options)?.GetValueKind() ?? JsonValueKind.Null; }
            catch (JsonException) { return JsonValueKind.String; }
        }

        private static string TypeOfNode(string field, JsonNode? node)
        {
            var lower = field.ToLowerInvariant();
            if (lower.Contains("time") || lower.Contains("date")) return "datetime";
            return node?.GetValueKind() switch
            {
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "switch",
                JsonValueKind.Object or JsonValueKind.Array => "json",
                _ => "input",
            };
        }

        private static object? NormalizeNode(JsonNode? node, string? field)
        {
            if (node is null) return null;

            var kind = node.GetValueKind();
            if (field is not null && (field.Contains("Time", StringComparison.OrdinalIgnoreCase)
                                      || field.Contains("Date", StringComparison.OrdinalIgnoreCase))
                                && kind == JsonValueKind.Number
                                && node is JsonValue timeValue && timeValue.TryGetValue<long>(out var ms)
                                && ms > 1_000_000_000_000L)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }

            return kind switch
            {
                JsonValueKind.String => Clamp(node.GetValue<string>(), MaxValueLength),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => node,
                JsonValueKind.Null => null,
                _ => Clamp(node.ToJsonString(Options), MaxValueLength),
            };
        }

        private static string Clamp(string? value, int maxLength, int totalItems = 0)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length <= maxLength) return value;
            return value[..maxLength] + (totalItems > 1 ? $"…（共 {totalItems} 项）" : "…");
        }
    }
}
