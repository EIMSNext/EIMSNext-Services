using System.Net;
using System.Reflection;
using System.Text;

using EIMSNext.ApiClient.Contracts;
using EIMSNext.ApiClient.File;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using RestSharp;

namespace EIMSNext.Flow.Tests
{
    /// <summary>
    /// FileApiClient 上传响应反序列化回归：File.Host /api/v1/Upload 的真实响应外壳是
    /// {"value":[{id,fileName,savePath,...}]}（camelCase）。DTO 必须是公共类型——RestSharp
    /// 反序列化私有嵌套类不可靠（参见 SystemTokenProvider.SystemTokenResponse 历史事故）。
    /// </summary>
    [TestClass]
    public class FileApiClientEnvelopeTests
    {
        private const string FileHostResponse = """
            {"value":[{"id":"0S2KFILE0001","fileName":"print_20261029.pdf","savePath":"upload/corp/e13b.pdf",
                       "thumbPath":"upload/corp/e13b_thumb.pdf","fileExt":".pdf","fileSize":27123}]}
            """;

        private sealed class StubHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(FileHostResponse, Encoding.UTF8, "application/json"),
                });
            }
        }

        [TestMethod]
        public async Task UploadFileAsync_Deserializes_PublicEnvelope_FromFileHostShape()
        {
            var options = new RestClientOptions("http://filehost.test")
            {
                ConfigureMessageHandler = _ => new StubHandler(),
            };
            using var client = new RestClient(options);
            var request = new RestRequest("api/v1/Upload", Method.Post);
            request.AddFile("files", new byte[] { 1, 2, 3 }, "print.pdf", "application/pdf");

            var response = await client.ExecuteAsync<FileUploadEnvelope>(request);

            Assert.IsNotNull(response.Data, "FileUploadEnvelope 反序列化失败（Data 为 null）");
            var item = response.Data.Value?.FirstOrDefault();
            Assert.IsNotNull(item);
            Assert.AreEqual("0S2KFILE0001", item.Id);
            Assert.AreEqual("print_20261029.pdf", item.FileName);
            Assert.AreEqual("upload/corp/e13b.pdf", item.SavePath);
            Assert.AreEqual(27123L, item.FileSize);
        }

        [TestMethod]
        public void FileApiClient_Must_Not_Hide_Envelope_As_PrivateType()
        {
            var privateNested = typeof(FileApiClient).GetNestedTypes(BindingFlags.NonPublic)
                .Where(t => t.Name.Contains("Envelope", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.AreEqual(0, privateNested.Count, "响应 envelope 不允许是私有类型: {0}",
                string.Join(",", privateNested.Select(t => t.Name)));

            var publicEnvelope = typeof(FileApiClient).Assembly.GetType("EIMSNext.ApiClient.File.FileUploadEnvelope");
            Assert.IsNotNull(publicEnvelope, "FileUploadEnvelope 必须存在于 ApiClient.File 程序集");
            Assert.IsTrue(publicEnvelope.IsPublic && publicEnvelope.IsSealed,
                "FileUploadEnvelope 必须是 public sealed");
        }

        [TestMethod]
        public void FileApiClientSetting_Binds_FileApiClient_Section()
        {
            // Setting 节名解析自 typeof(TClient).Name，必须与各宿主 appsettings 的 "FileApiClient" 节一致。
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileApiClient:BaseUrl"] = "https://localhost:8855",
                })
                .Build();
            var client = new FileApiClient(config, NullLogger<FileApiClient>.Instance);

            var settingProperty = typeof(RestApiClientBase<FileApiClient, FileApiClientSetting>)
                .GetProperty("Setting", BindingFlags.NonPublic | BindingFlags.Instance);
            var setting = (FileApiClientSetting?)settingProperty?.GetValue(client);

            Assert.IsNotNull(setting);
            Assert.AreEqual("https://localhost:8855", setting.BaseUrl);
            Assert.IsTrue(setting.Verify());
        }
    }
}
