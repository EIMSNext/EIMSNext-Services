using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Nodes;

using EIMSNext.Common.Extensions;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Service
{
    /// <summary>
    /// Keeps attachment reference counts in sync with persisted form data.
    /// Stored attachment URLs are always relative storage paths.
    /// </summary>
    internal sealed class AttachmentReferenceService
    {
        private static readonly IReadOnlyDictionary<string, int> EmptyCounts = new Dictionary<string, int>();
        private readonly IResolver _resolver;
        private IRepository<UploadedFile>? _repository;
        private IRepository<FormDef>? _formRepository;

        public AttachmentReferenceService(IResolver resolver)
        {
            _resolver = resolver;
        }

        public void Apply(FormData entity, FormData? old)
        {
            ApplyDelta(old == null ? [] : Count(old), Count(entity));
        }

        public void Release(IEnumerable<FormData> entities)
        {
            foreach (var entity in entities)
            {
                ApplyDelta(Count(entity), EmptyCounts);
            }
        }

        /// <summary>
        /// 按引用计数增量同步 <see cref="UploadedFile.RefCount"/>。
        /// Mongo 时期靠服务端 $inc 保证原子性；EF Core 下改为「条件 UPDATE + 回退钳制」两步：
        /// 先尝试带下界守卫的原子自减，未命中说明会减成负数，再单独把计数钳到 0。
        /// </summary>
        /// <param name="oldCounts">变更前的引用计数。</param>
        /// <param name="newCounts">变更后的引用计数。</param>
        private void ApplyDelta(IReadOnlyDictionary<string, int> oldCounts, IReadOnlyDictionary<string, int> newCounts)
        {
            foreach (var id in oldCounts.Keys.Concat(newCounts.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var delta = newCounts.GetValueOrDefault(id) - oldCounts.GetValueOrDefault(id);
                if (delta == 0) continue;

                var repository = _repository ??= _resolver.GetRepository<UploadedFile>();
                if (delta > 0)
                {
                    repository.UpdateMany(
                        x => x.Id == id,
                        setters => setters.SetProperty(x => x.RefCount, x => x.RefCount + delta));
                    continue;
                }

                var decrement = -delta;
                var affected = repository.UpdateMany(
                    x => x.Id == id && x.RefCount >= decrement,
                    setters => setters.SetProperty(x => x.RefCount, x => x.RefCount - decrement));
                if (affected == 0)
                {
                    // 引用数不足，直接钳到 0，避免出现负数计数。
                    repository.UpdateMany(
                        x => x.Id == id && x.RefCount < decrement,
                        setters => setters.SetProperty(x => x.RefCount, 0));
                }
            }
        }

        private Dictionary<string, int> Count(FormData entity)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var form = GetForm(entity.FormId);
            if (form?.Content?.Items == null) return counts;

            var root = JsonNode.Parse(JsonSerializer.Serialize(entity.Data));
            foreach (var field in form.Content.Items)
            {
                CountField(root, field, counts);
            }
            return counts;
        }

        private FormDef? GetForm(string formId)
        {
            if (string.IsNullOrWhiteSpace(formId)) return null;
            try
            {
                return (_formRepository ??= _resolver.GetRepository<FormDef>()).Get(formId);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private static void CountField(JsonNode? root, FieldDef field, IDictionary<string, int> counts)
        {
            if (string.IsNullOrWhiteSpace(field.Field) || root is not JsonObject obj) return;
            if (!TryGetProperty(obj, field.Field, out var value)) return;

            if (string.Equals(field.Type, FieldType.FileUpload, StringComparison.OrdinalIgnoreCase)
                || string.Equals(field.Type, FieldType.ImageUpload, StringComparison.OrdinalIgnoreCase))
            {
                CountAttachmentValue(value, counts);
                return;
            }

            if (string.Equals(field.Type, FieldType.TableForm, StringComparison.OrdinalIgnoreCase)
                && field.Columns != null
                && value is JsonArray rows)
            {
                foreach (var row in rows)
                {
                    foreach (var column in field.Columns)
                    {
                        CountField(row, column, counts);
                    }
                }
            }
        }

        private static void CountAttachmentValue(JsonNode? value, IDictionary<string, int> counts)
        {
            if (value is JsonArray array)
            {
                foreach (var item in array) CountAttachmentValue(item, counts);
                return;
            }

            if (value is JsonObject obj && IsAttachment(obj) && TryGetString(obj, "id", out var id))
            {
                counts.TryGetValue(id, out var current);
                counts[id] = current + 1;
            }
        }

        private static bool IsAttachment(JsonObject obj)
        {
            return TryGetString(obj, "id", out _)
                && TryGetString(obj, "url", out _)
                && (TryGetString(obj, "name", out _) || TryGetString(obj, "fileName", out _));
        }

        private static bool TryGetProperty(JsonObject obj, string key, out JsonNode? value)
        {
            var item = obj.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            value = item.Value;
            return item.Key != null;
        }

        private static bool TryGetString(JsonObject obj, string key, out string value)
        {
            var item = obj.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            value = item.Value?.ToString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(value);
        }
    }
}
