using EIMSNext.Async.Quartz;

using HKH.Mef2.Integration;

using Microsoft.Extensions.DependencyInjection;

using Quartz;

using System.Composition.Hosting;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class QuartzJobScopeTests
    {
        [TestMethod]
        public async Task QuartzJob_UsesNewScopePerExecution()
        {
            var observedScopeIds = new List<Guid>();
            var jobKey = new JobKey("ScopedTestJob", "Tests");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<ScopeMarker>();
            services.AddScoped<IResolver, TestResolver>();
            services.AddSingleton(observedScopeIds);
            services.AddQuartz(q =>
            {
                q.AddJob<ScopedTestJob>(opts => opts.StoreDurably().WithIdentity(jobKey));
            });

            await using var provider = services.BuildServiceProvider();
            var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
            var scheduler = await schedulerFactory.GetScheduler();

            await scheduler.Start();
            try
            {
                await scheduler.TriggerJob(jobKey);
                await WaitForExecutionsAsync(observedScopeIds, 1);

                await scheduler.TriggerJob(jobKey);
                await WaitForExecutionsAsync(observedScopeIds, 2);
            }
            finally
            {
                await scheduler.Shutdown(waitForJobsToComplete: true);
            }

            Assert.AreEqual(2, observedScopeIds.Count);
            Assert.AreNotEqual(observedScopeIds[0], observedScopeIds[1]);
        }

        private static async Task WaitForExecutionsAsync(List<Guid> observedScopeIds, int expectedCount)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (observedScopeIds.Count < expectedCount && DateTime.UtcNow < timeout)
            {
                await Task.Delay(50);
            }

            Assert.AreEqual(expectedCount, observedScopeIds.Count);
        }

        private sealed class ScopedTestJob(IResolver resolver, List<Guid> observedScopeIds) : JobBase<ScopedTestJob>(resolver)
        {
            private readonly List<Guid> _observedScopeIds = observedScopeIds;

            protected override Task ExecuteAsync(IJobExecutionContext context)
            {
                _observedScopeIds.Add(Resolver.Resolve<ScopeMarker>().Id);
                return Task.CompletedTask;
            }
        }

        private sealed class ScopeMarker
        {
            public Guid Id { get; } = Guid.NewGuid();
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
