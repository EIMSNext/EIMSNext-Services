using Asp.Versioning;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using NanoidDotNet;
using RestSharp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class WebhookController(IResolver resolver) : ApiControllerBase<WebhookApiService, Webhook, WebhookViewModel>(resolver)
    {
        [HttpPost("Test")]
        public async Task<IActionResult> TestAsync([FromBody]WebhookRequest request)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(request.AppId);
            if (!string.IsNullOrWhiteSpace(request.FormId))
            {
                var form = Resolver.GetService<FormDef>().Get(request.FormId);
                if (form == null || form.CorpId != IdentityContext.CurrentCorpId || form.DeleteFlag || form.AppId != request.AppId)
                {
                    return ApiResult.Fail(400, "推送表单不存在").ToActionResult();
                }
            }

            if (string.IsNullOrWhiteSpace(request.Url))
                return ApiResult.Fail(400, "服务器地址不能为空").ToActionResult();

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var webhookUri) ||
                string.IsNullOrWhiteSpace(webhookUri.Host) ||
                (webhookUri.Scheme != Uri.UriSchemeHttp && webhookUri.Scheme != Uri.UriSchemeHttps))
            {
                return ApiResult.Fail(400, "服务器地址必须是有效的 HTTP 或 HTTPS 地址").ToActionResult();
            }

            try
            {
                using var client = new RestClient(new RestClientOptions(webhookUri)
                {
                    Timeout = TimeSpan.FromSeconds(10),
                });

                var challenge = Nanoid.Generate(size: 8);
                var response = await client.ExecuteAsync(BuildRequest(challenge, request.Secret));

                if (response.IsSuccessful)
                {
                    return ApiResult.Success(new
                    {
                        success = true,
                        statusCode = (int)response.StatusCode,
                        message = "连接测试成功"
                    }).ToActionResult();
                }

                return ApiResult.Fail(400, $"连接测试失败: {(int)response.StatusCode} {response.StatusDescription ?? response.ErrorMessage ?? "请求未成功"}").ToActionResult();
            }
            catch (Exception ex)
            {
                return ApiResult.Fail(500, $"连接测试失败: {ex.Message}").ToActionResult();
            }
        }

        private RestRequest BuildRequest(string challenge, string? secret)
        {
            var request = new RestRequest(string.Empty, Method.Post);
            var payload = new { Challenge = challenge };
            var payloadJson = JsonSerializer.Serialize(payload);

            request.AddHeader("Content-Type", "application/json; charset=utf-8");

            if (!string.IsNullOrWhiteSpace(secret))
            {
                request.AddHeader("X-EIMS-Signature", ComputeSignature(payloadJson, secret));
            }

            request.AddStringBody(payloadJson, DataFormat.Json);

            return request;
        }

        private static string ComputeSignature(string payload, string secret)
        {
            var key = Encoding.UTF8.GetBytes(secret);
            var body = Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(body);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
