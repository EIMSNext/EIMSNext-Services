using System.Text.Json;
using EIMSNext.ApiCore;
using HKH.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace EIMSNext.Identity.Tests
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
                    new TestWebHostEnvironment(Environments.Production),
                    NullLogger<ExceptionFilterMiddleware>.Instance);

                await middleware.Invoke(context);

                Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
                context.Response.Body.Position = 0;
                var payload = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
                Assert.AreEqual("badrequest", payload.GetProperty("statecode").GetString());
                Assert.AreEqual("请求格式不合法或超出限制", payload.GetProperty("message").GetString());
            }
        }

        [TestMethod]
        public async Task Invoke_InternalException_ProductionHidesDetails_DevelopmentShowsThem()
        {
            var exception = new InvalidOperationException("42P01: relation \"Client\" does not exist");

            var production = await InvokeWithEnvironment(Environments.Production, exception);
            Assert.AreEqual(StatusCodes.Status500InternalServerError, production.StatusCode);
            Assert.AreEqual("internalservererror", production.Payload.GetProperty("statecode").GetString());
            Assert.AreEqual("抱歉，出错了", production.Payload.GetProperty("message").GetString());

            var development = await InvokeWithEnvironment(Environments.Development, exception);
            Assert.AreEqual(StatusCodes.Status500InternalServerError, development.StatusCode);
            Assert.AreEqual("internalservererror", development.Payload.GetProperty("statecode").GetString());
            Assert.AreEqual("42P01: relation \"Client\" does not exist", development.Payload.GetProperty("message").GetString());
        }

        [TestMethod]
        public async Task Invoke_UnLogException_ReturnsBusinessMessage_InEveryEnvironment()
        {
            var exception = new UnLogException("流程启动失败: 审批人为空");

            foreach (var environmentName in new[] { Environments.Production, Environments.Development })
            {
                var result = await InvokeWithEnvironment(environmentName, exception);
                Assert.AreEqual(StatusCodes.Status500InternalServerError, result.StatusCode);
                Assert.AreEqual("internalservererror", result.Payload.GetProperty("statecode").GetString());
                Assert.AreEqual("流程启动失败: 审批人为空", result.Payload.GetProperty("message").GetString());
            }
        }

        private static async Task<(int StatusCode, JsonElement Payload)> InvokeWithEnvironment(string environmentName, Exception exception)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var middleware = new ExceptionFilterMiddleware(
                _ => Task.FromException(exception),
                new TestWebHostEnvironment(environmentName),
                NullLogger<ExceptionFilterMiddleware>.Instance);

            await middleware.Invoke(context);

            context.Response.Body.Position = 0;
            var payload = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
            return (context.Response.StatusCode, payload!);        }

        private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
        {
            public string ApplicationName { get; set; } = "EIMSNext.Tests";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get; set; } = string.Empty;
            public string EnvironmentName { get; set; } = environmentName;
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
