using System.Text.Json;
using System.Text.Json.Nodes;
using EIMSNext.Common;
using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService
{
    public static class PublicFormSystemFieldHelper
    {
        public const string Source = "public";
        public const string WxOpenId = "wxopenid";
        public const string WxNickname = "wxnickname";
        public const string WxAvator = "wxavator";
        public const string Ext = "ext";

        private static readonly IReadOnlyDictionary<string, PublicSystemFieldSpec> Specs =
            new Dictionary<string, PublicSystemFieldSpec>(StringComparer.OrdinalIgnoreCase)
            {
                [WxOpenId] = new(WxOpenId, "微信 OpenId", FieldType.Input),
                [WxNickname] = new(WxNickname, "微信昵称", FieldType.Input),
                [WxAvator] = new(WxAvator, "微信头像", FieldType.Signature),
                [Ext] = new(Ext, "扩展字段", FieldType.Input),
            };

        public static void EnsureRequiredFields(FormDef formDef, PublicSetting setting)
        {
            if (setting.TargetType != PublicTargetType.Form)
            {
                return;
            }

            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (setting.Form.FormLink.Wechat.Enabled)
            {
                required.Add(WxOpenId);
                if (setting.Form.FormLink.Wechat.AcquireMode == PublicWechatAcquireMode.ExplicitGrant)
                {
                    required.Add(WxNickname);
                    required.Add(WxAvator);
                }
            }

            if (setting.Form.FormLink.ExtLink.Enabled)
            {
                required.Add(Ext);
            }

            EnsureFields(formDef, required);
        }

        public static void EnsureExistingPublicFields(FormDef formDef, FormContent? oldContent)
        {
            var oldLayout = ParseLayout(oldContent?.Layout);
            if (oldLayout == null || oldLayout.Count == 0)
            {
                return;
            }

            var existingPublicFields = FindPublicSystemFields(oldLayout)
                .Select(x => x.Field)
                .Where(x => Specs.ContainsKey(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            EnsureFields(formDef, existingPublicFields);
        }

        public static bool IsPublicSystemField(string? field)
        {
            return !string.IsNullOrWhiteSpace(field) && Specs.ContainsKey(field);
        }

        private static void EnsureFields(FormDef formDef, IEnumerable<string> fields)
        {
            var required = fields.Where(IsPublicSystemField).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (required.Count == 0)
            {
                return;
            }

            var layout = ParseLayout(formDef.Content?.Layout) ?? [];
            foreach (var field in required)
            {
                EnsureField(layout, Specs[field]);
            }

            formDef.Content ??= new FormContent();
            formDef.Content.Layout = layout.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }

        private static void EnsureField(JsonArray layout, PublicSystemFieldSpec spec)
        {
            var existing = FindField(layout, spec.Field);
            if (existing == null)
            {
                layout.Add(CreateNode(spec));
                return;
            }

            existing["type"] = spec.Type;
            existing["field"] = spec.Field;
            existing["title"] = spec.Title;
            existing["hidden"] = true;
            existing["source"] = Source;
            existing["systemKind"] = spec.Field;
        }

        private static JsonObject CreateNode(PublicSystemFieldSpec spec)
        {
            return new JsonObject
            {
                ["type"] = spec.Type,
                ["field"] = spec.Field,
                ["title"] = spec.Title,
                ["hidden"] = true,
                ["source"] = Source,
                ["systemKind"] = spec.Field,
                ["props"] = new JsonObject()
            };
        }

        private static JsonArray? ParseLayout(string? layout)
        {
            if (string.IsNullOrWhiteSpace(layout))
            {
                return new JsonArray();
            }

            try
            {
                return JsonNode.Parse(layout) as JsonArray ?? new JsonArray();
            }
            catch
            {
                return new JsonArray();
            }
        }

        private static JsonObject? FindField(JsonArray? nodes, string field)
        {
            if (nodes == null)
            {
                return null;
            }

            foreach (var node in nodes)
            {
                if (node is not JsonObject obj)
                {
                    continue;
                }

                var currentField = GetStringValue(obj, "field");
                if (string.Equals(currentField, field, StringComparison.OrdinalIgnoreCase))
                {
                    return obj;
                }

                if (obj.TryGetPropertyValue("children", out var childrenNode) && childrenNode is JsonArray children)
                {
                    var child = FindField(children, field);
                    if (child != null)
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<PublicSystemFieldSpec> FindPublicSystemFields(JsonArray? nodes)
        {
            if (nodes == null)
            {
                yield break;
            }

            foreach (var node in nodes)
            {
                if (node is not JsonObject obj)
                {
                    continue;
                }

                var field = GetStringValue(obj, "field");
                if (!string.IsNullOrWhiteSpace(field))
                {
                    var source = GetStringValue(obj, "source");
                    var systemKind = GetStringValue(obj, "systemKind");
                    if (Specs.ContainsKey(field) &&
                        (string.Equals(source, Source, StringComparison.OrdinalIgnoreCase) ||
                         !string.IsNullOrWhiteSpace(systemKind)))
                    {
                        yield return Specs[field];
                    }
                }

                if (obj.TryGetPropertyValue("children", out var childrenNode) && childrenNode is JsonArray children)
                {
                    foreach (var child in FindPublicSystemFields(children))
                    {
                        yield return child;
                    }
                }
            }
        }

        private static string? GetStringValue(JsonObject obj, string propertyName)
        {
            if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            {
                return null;
            }

            try
            {
                return value.GetValue<string>();
            }
            catch
            {
                return null;
            }
        }

        private sealed record PublicSystemFieldSpec(string Field, string Title, string Type);
    }
}
