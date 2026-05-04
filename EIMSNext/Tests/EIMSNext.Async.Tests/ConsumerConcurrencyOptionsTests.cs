using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Tasks.Consumers;
using EIMSNext.CloudEvent;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

using System.Composition.Hosting;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class ConsumerConcurrencyOptionsTests
    {
        [TestMethod]
        public void TaskConsumerBase_UsesConfiguredQueueConcurrency()
        {
            var services = BuildServices(options =>
            {
                options.Queues["webhook"] = new QueueConcurrencyOptions
                {
                    Concurrency = 5,
                    PrefetchCount = 2
                };
            });

            using var provider = services.BuildServiceProvider();
            var consumer = new WebhookConsumer(provider.GetRequiredService<IServiceScopeFactory>());
            var queueOptions = consumer.GetQueueOptions();

            Assert.AreEqual(5, queueOptions.Concurrency);
            Assert.AreEqual((ushort)2, queueOptions.PrefetchCount);
        }

        [TestMethod]
        public void TaskConsumerBase_UsesDefaultQueueConcurrencyWhenMissing()
        {
            var services = BuildServices(_ => { });

            using var provider = services.BuildServiceProvider();
            var consumer = new WebhookConsumer(provider.GetRequiredService<IServiceScopeFactory>());
            var queueOptions = consumer.GetQueueOptions();

            Assert.AreEqual(1, queueOptions.Concurrency);
            Assert.AreEqual((ushort)1, queueOptions.PrefetchCount);
        }

        private static ServiceCollection BuildServices(Action<ConsumerConcurrencyOptions> configureOptions)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, AttributeMessageRouteResolver>();
            services.AddScoped<IResolver, TestResolver>();
            services.AddScoped<IEventHub, FakeEventHub>();
            services.AddOptions<ConsumerConcurrencyOptions>().Configure(configureOptions);

            return services;
        }

        private sealed class FakeEventHub : IEventHub
        {
            public Task SendAsync(Webhook webhook, WebHookTrigger trigger, object data) => Task.CompletedTask;
        }

        private sealed class TestResolver(IServiceProvider serviceProvider) : IResolver
        {
            public CompositionContainer MefContainer => throw new NotSupportedException();

            public object Resolve(Type type, string? name = null) => serviceProvider.GetRequiredService(type);

            public T Resolve<T>(string? name = null) where T : class => serviceProvider.GetRequiredService<T>();

            public T GetExport<T>(string? name = null) where T : class => serviceProvider.GetRequiredService<T>();

            public object GetExport(Type type, string? name = null) => serviceProvider.GetRequiredService(type);

            public IEnumerable<T> GetExports<T>(string? name = null) where T : class => serviceProvider.GetServices<T>();

            public IEnumerable<object> GetExports(Type type, string? name = null) => serviceProvider.GetServices(type).Cast<object>();
        }
    }
}
