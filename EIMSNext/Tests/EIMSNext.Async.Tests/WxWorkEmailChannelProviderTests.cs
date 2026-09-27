using System.Collections.Concurrent;
using System.Composition.Hosting;
using System.Net;
using System.Net.Http;

using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.Tasks.Email;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EIMSNext.Async.Tests;

[TestClass]
public class WxWorkEmailChannelProviderTests
{
    [TestMethod]
    public async Task SendAsync_DevelopmentAcceptsWxWorkApiErrorAndCachesToken()
    {
        var handler = new RecordingHandler(
            JsonResponse("{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"token-1\",\"expires_in\":7200}"),
            JsonResponse("{\"errcode\":60020,\"errmsg\":\"recipient unavailable\"}"),
            JsonResponse("{\"errcode\":60020,\"errmsg\":\"recipient unavailable\"}"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://qyapi.weixin.qq.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WxWorkEmailChannelProvider(client);
        var resolver = CreateResolver(cache);
        var task = CreateTask();

        await provider.SendAsync(task, ["liwentao@dongyuntech.cn"], resolver, CancellationToken.None);
        await provider.SendAsync(task, ["liwentao@dongyuntech.cn"], resolver, CancellationToken.None);

        Assert.AreEqual(3, handler.RequestUris.Count);
        Assert.AreEqual(1, handler.RequestUris.Count(x => x.Contains("cgi-bin/gettoken", StringComparison.Ordinal)));
        Assert.AreEqual(2, handler.RequestUris.Count(x => x.Contains("compose_send", StringComparison.Ordinal)));
        foreach (var body in handler.Bodies.Where(x => x.Contains("liwentao@dongyuntech.cn", StringComparison.Ordinal)))
        {
            StringAssert.Contains(body, "\"content_type\":\"html\"");
        }
    }

    [TestMethod]
    public async Task SendAsync_InvalidTokenRefreshesOnceBeforeAcceptingDevelopmentApiError()
    {
        var handler = new RecordingHandler(
            JsonResponse("{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"token-1\",\"expires_in\":7200}"),
            JsonResponse("{\"errcode\":40014,\"errmsg\":\"invalid token\"}"),
            JsonResponse("{\"errcode\":0,\"errmsg\":\"ok\",\"access_token\":\"token-2\",\"expires_in\":7200}"),
            JsonResponse("{\"errcode\":60020,\"errmsg\":\"recipient unavailable\"}"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://qyapi.weixin.qq.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WxWorkEmailChannelProvider(client);

        await provider.SendAsync(CreateTask(), ["liwentao@dongyuntech.cn"], CreateResolver(cache), CancellationToken.None);

        Assert.AreEqual(2, handler.RequestUris.Count(x => x.Contains("cgi-bin/gettoken", StringComparison.Ordinal)));
        Assert.AreEqual(2, handler.RequestUris.Count(x => x.Contains("compose_send", StringComparison.Ordinal)));
    }

    private static EmailNotifyTaskArgs CreateTask()
    {
        return new EmailNotifyTaskArgs
        {
            TaskType = EmailTaskType.PlatWork,
            NotifyId = "corp-1",
            Title = "New corporate",
            Detail = "<strong>Corporate</strong>"
        };
    }

    private static IResolver CreateResolver(IMemoryCache cache)
    {
        return new TestResolver(new Dictionary<Type, object>
        {
            [typeof(IMemoryCache)] = cache,
            [typeof(IHostEnvironment)] = new TestHostEnvironment(),
            [typeof(ILogger<WxWorkEmailChannelProvider>)] = NullLogger<WxWorkEmailChannelProvider>.Instance
        });
    }

    private static HttpResponseMessage JsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public ConcurrentBag<string> RequestUris { get; } = [];

        public ConcurrentBag<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!.ToString());
            if (request.Content != null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return _responses.Dequeue();
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestResolver(IReadOnlyDictionary<Type, object> services) : IResolver
    {
        public CompositionContainer MefContainer => throw new NotSupportedException();

        public object Resolve(Type type, string? name = null) => services[type];

        public T Resolve<T>(string? name = null) where T : class => (T)services[typeof(T)];

        public T GetExport<T>(string? name = null) where T : class => throw new NotSupportedException();

        public object GetExport(Type type, string? name = null) => throw new NotSupportedException();

        public IEnumerable<T> GetExports<T>(string? name = null) where T : class => throw new NotSupportedException();

        public IEnumerable<object> GetExports(Type type, string? name = null) => throw new NotSupportedException();
    }
}
