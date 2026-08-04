using System.Text.Json;
using EIMSNext.ApiCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace EIMSNext.Auth.Tests
{
    [TestClass]
    public class ExceptionFilterMiddlewareTests
    {
        [TestMethod]
        public async Task Invoke_MapsInvalidRequestPayloadExceptionsToBadRequest()
        {
            foreach (var exception in new Exception[]
                     {
                         new BadHttpRequestException("invalid multipart request"),
                         new InvalidDataException("Multipart headers length limit exceeded")
                     })
            {
                var context = new DefaultHttpContext();
                context.Response.Body = new MemoryStream();
                var middleware = new ExceptionFilterMiddleware(
                    _ => Task.FromException(exception),
                    new TestWebHostEnvironment(),
                    NullLogger<ExceptionFilterMiddleware>.Instance);

                await middleware.Invoke(context);

                Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
                context.Response.Body.Position = 0;
                var payload = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
                Assert.AreEqual("badrequest", payload.GetProperty("statecode").GetString());
                Assert.AreEqual("请求格式不合法或超出限制", payload.GetProperty("message").GetString());
            }
        }

        private sealed class TestWebHostEnvironment : IWebHostEnvironment
        {
            public string ApplicationName { get; set; } = "EIMSNext.Tests";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get; set; } = string.Empty;
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
