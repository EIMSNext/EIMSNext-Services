using System.Dynamic;
using System.Text.Json;

using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Extensions;
using EIMSNext.Core.Repositories;
using EIMSNext.Flow.Core.Interfaces;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.Flow.Service
{
    /// <summary>
    /// 数据流HTTP触发服务实现。
    /// </summary>
    public class DataflowHookService(IResolver resolver) : IDataflowHookService
    {
        private readonly IRepository<Wf_Definition> _definitionRepository = resolver.GetRepository<Wf_Definition>();
        private readonly IRepository<DataflowHookSample> _sampleRepository = resolver.GetRepository<DataflowHookSample>();
        private readonly IDataflowRunner _dataflowRunner = resolver.Resolve<IDataflowRunner>();

        /// <inheritdoc />
        public async Task<(int StatusCode, string ContentType, string Body)> HandleAsync(string corpId, string dataflowId, string clientIp, string method, string contentType, Dictionary<string, string> headers, string body)
        {
            var definition = _definitionRepository.Queryable.FirstOrDefault(x => x.CorpId == corpId && x.Id == dataflowId && !x.Disabled)
                ?? throw new NotFoundException("智能助手不存在或已禁用");

            if (definition.EventSource != EventSourceType.Http)
            {
                throw new BadRequestException("当前智能助手不是HTTP触发");
            }

            var triggerSetting = definition.Metadata.Steps.FirstOrDefault()?.DfNodeSetting?.TriggerSetting
                ?? throw new BadRequestException("HTTP触发配置不存在");

            ValidateIp(clientIp, triggerSetting.HttpTrigger);

            var requestContext = BuildRequestContext(clientIp, headers, body);
            await SaveSampleAsync(definition, requestContext);
            await RunHttpDataflowAsync(definition, requestContext);

            return (
                triggerSetting.HttpTrigger?.ResponseEnabled == true ? triggerSetting.HttpTrigger.ResponseStatusCode ?? 200 : 200,
                triggerSetting.HttpTrigger?.ResponseEnabled == true ? triggerSetting.HttpTrigger.ResponseContentType ?? "application/json" : "application/json",
                triggerSetting.HttpTrigger?.ResponseEnabled == true ? triggerSetting.HttpTrigger.ResponseBody ?? "{}" : "{}"
            );
        }

        /// <inheritdoc />
        public Task<DataflowHookSample?> GetLatestSampleAsync(string corpId, string dataflowId)
        {
            return Task.FromResult(_sampleRepository.Queryable
                .Where(x => x.CorpId == corpId && x.DataflowId == dataflowId)
                .OrderByDescending(x => x.CapturedAt)
                .FirstOrDefault());
        }

        private static void ValidateIp(string clientIp, DataflowHttpTriggerSetting? httpTrigger)
        {
            var allowedIps = httpTrigger?.AllowedIps?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            if (allowedIps == null || allowedIps.Count == 0)
            {
                return;
            }

            // 支持精确 IP、通配符（10.0.0.*）与 CIDR（10.0.0.0/24）
            if (!EIMSNext.Common.IpMatcher.IsAllowed(clientIp, allowedIps))
            {
                throw new ForbiddenException("当前IP不在白名单中");
            }
        }

        private DataflowHttpRequestContext BuildRequestContext(string clientIp, Dictionary<string, string> headers, string body)
        {
            var headerData = headers.ToDictionary(x => x.Key, x => ToScalarValue(x.Value));
            var bodyData = ParseBody(body);
            var requestRoot = new Dictionary<string, object?>
            {
                ["header"] = headerData,
                ["body"] = bodyData,
                ["ip"] = clientIp,
            };

            var fields = new List<DataflowHttpSampleField>();
            Flatten("header", headerData, fields);
            Flatten("body", bodyData, fields);
            fields.Add(new DataflowHttpSampleField { Key = "ip", Label = "ip", Type = "text", SampleValue = clientIp });

            return new DataflowHttpRequestContext
            {
                ClientIp = clientIp,
                Header = headerData,
                Body = bodyData,
                Fields = fields,
                RawJson = JsonSerializer.Serialize(requestRoot)
            };
        }

        private static Dictionary<string, object?> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return [];
            }

            try
            {
                var node = JsonSerializer.Deserialize<JsonElement>(body);
                if (node.ValueKind == JsonValueKind.Object)
                {
                    return ParseObject(node);
                }

                return new Dictionary<string, object?> { ["value"] = ToScalarValue(node) };
            }
            catch
            {
                return new Dictionary<string, object?> { ["value"] = body };
            }
        }

        private static Dictionary<string, object?> ParseObject(JsonElement element)
        {
            var result = new Dictionary<string, object?>();
            foreach (var property in element.EnumerateObject())
            {
                result[property.Name] = ParseElement(property.Value);
            }

            return result;
        }

        private static object? ParseElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ParseObject(element),
                JsonValueKind.Array => ParseArray(element),
                _ => ToScalarValue(element),
            };
        }

        private static object ParseArray(JsonElement element)
        {
            var items = element.EnumerateArray().ToList();
            if (items.Count == 0)
            {
                return string.Empty;
            }

            if (items.All(x => x.ValueKind != JsonValueKind.Object && x.ValueKind != JsonValueKind.Array))
            {
                return string.Join(',', items.Select(ToScalarValue).Where(x => x != null).Select(x => x!.ToString()));
            }

            var lastObject = items.LastOrDefault(x => x.ValueKind == JsonValueKind.Object);
            return lastObject.ValueKind == JsonValueKind.Object ? ParseObject(lastObject) : JsonSerializer.Serialize(items.Last());
        }

        private static object? ToScalarValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null => string.Empty,
                _ => element.ToString(),
            };
        }

        private static object? ToScalarValue(string value)
        {
            if (long.TryParse(value, out var longValue))
            {
                return longValue;
            }

            if (double.TryParse(value, out var doubleValue))
            {
                return doubleValue;
            }

            return value;
        }

        private static void Flatten(string prefix, object? value, List<DataflowHttpSampleField> fields)
        {
            switch (value)
            {
                case Dictionary<string, object?> dict:
                    fields.Add(new DataflowHttpSampleField
                    {
                        Key = prefix,
                        Label = prefix,
                        Type = "text",
                        SampleValue = JsonSerializer.Serialize(dict)
                    });
                    foreach (var item in dict)
                    {
                        Flatten($"{prefix}_{item.Key}", item.Value, fields);
                    }
                    break;
                case long or int or double or decimal:
                    fields.Add(new DataflowHttpSampleField
                    {
                        Key = prefix,
                        Label = prefix,
                        Type = "number",
                        SampleValue = value?.ToString()
                    });
                    break;
                default:
                    fields.Add(new DataflowHttpSampleField
                    {
                        Key = prefix,
                        Label = prefix,
                        Type = "text",
                        SampleValue = value?.ToString() ?? string.Empty
                    });
                    break;
            }
        }

        private async Task SaveSampleAsync(Wf_Definition definition, DataflowHttpRequestContext requestContext)
        {
            var sample = new DataflowHookSample
            {
                CorpId = definition.CorpId,
                AppId = definition.AppId,
                DataflowId = definition.Id,
                ClientIp = requestContext.ClientIp,
                RawJson = requestContext.RawJson,
                FlattenedFieldsJson = JsonSerializer.Serialize(requestContext.Fields),
                CapturedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            await _sampleRepository.InsertAsync(sample);
            var triggerSetting = definition.Metadata.Steps.First().DfNodeSetting!.TriggerSetting!;
            triggerSetting.HttpTrigger ??= new DataflowHttpTriggerSetting();
            triggerSetting.HttpTrigger.SampleCapturedAt = sample.CapturedAt;
            triggerSetting.HttpTrigger.SampleFields = requestContext.Fields;
            await _definitionRepository.ReplaceAsync(definition);
        }

        private Task RunHttpDataflowAsync(Wf_Definition definition, DataflowHttpRequestContext requestContext)
        {
            var data = new FormData
            {
                AppId = definition.AppId,
                CorpId = definition.CorpId,
                FormId = definition.SourceId ?? string.Empty,
                Data = ToExpando(requestContext),
            };

            return _dataflowRunner.RunAsync(new DfRunParamter(string.Empty, string.Empty, data, EventSourceType.Http, EventType.None, string.Empty, null, CascadeMode.All, null)
                .WithDataflowId(definition.Id));
        }

        private static ExpandoObject ToExpando(DataflowHttpRequestContext requestContext)
        {
            IDictionary<string, object?> expando = new ExpandoObject();
            expando["header"] = ToExpando(requestContext.Header);
            expando["body"] = ToExpando(requestContext.Body);
            expando["ip"] = requestContext.ClientIp;
            return (ExpandoObject)expando;
        }

        private static ExpandoObject ToExpando(Dictionary<string, object?> value)
        {
            IDictionary<string, object?> expando = new ExpandoObject();
            foreach (var item in value)
            {
                expando[item.Key] = item.Value is Dictionary<string, object?> dict ? ToExpando(dict) : item.Value;
            }

            return (ExpandoObject)expando;
        }
    }
}
