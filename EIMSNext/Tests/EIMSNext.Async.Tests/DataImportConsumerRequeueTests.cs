using System.Composition.Hosting;
using System.Linq.Expressions;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Tasks.Consumers;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class DataImportConsumerRequeueTests
    {
        [TestMethod]
        public async Task HandleAsync_WhenProcessingStateNotAcquired_ThrowsTaskRequeueException()
        {
            var importLogService = new FakeImportLogService
            {
                ImportLog = new FormDataImportLog
                {
                    Id = "log-1",
                    CorpId = "corp-1",
                    RetryCount = 0,
                    Status = FormDataImportStatus.Pending,
                },
                MarkProcessingResult = false,
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddSingleton<IFormDataImportLogService>(importLogService);
            services.AddScoped<IResolver, TestResolver>();

            await using var provider = services.BuildServiceProvider();
            var consumer = new TestableDataImportConsumer(provider.GetRequiredService<IServiceScopeFactory>());

            try
            {
                await consumer.InvokeAsync(new DataImportTaskArgs
                {
                    ImportLogId = "log-1",
                    CorpId = "corp-1",
                    RetryCount = 0,
                }, provider.GetRequiredService<IResolver>());
                Assert.Fail("Expected TaskRequeueException.");
            }
            catch (TaskRequeueException)
            {
            }

            Assert.AreEqual(0, importLogService.MarkFailedCalls);
        }

        [TestMethod]
        public async Task HandleAsync_WhenCancellationRequested_RethrowsWithoutMarkingFailed()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var importLogService = new FakeImportLogService
            {
                ImportLog = new FormDataImportLog
                {
                    Id = "log-1",
                    CorpId = "corp-1",
                    RetryCount = 0,
                    Status = FormDataImportStatus.Pending,
                },
                MarkProcessingResult = true,
                MarkProcessingException = new OperationCanceledException(cts.Token),
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddSingleton<IFormDataImportLogService>(importLogService);
            services.AddScoped<IResolver, TestResolver>();

            await using var provider = services.BuildServiceProvider();
            var consumer = new TestableDataImportConsumer(provider.GetRequiredService<IServiceScopeFactory>());

            try
            {
                await consumer.InvokeAsync(new DataImportTaskArgs
                {
                    ImportLogId = "log-1",
                    CorpId = "corp-1",
                    RetryCount = 0,
                }, provider.GetRequiredService<IResolver>(), cts.Token);
                Assert.Fail("Expected OperationCanceledException.");
            }
            catch (OperationCanceledException)
            {
            }

            Assert.AreEqual(0, importLogService.MarkFailedCalls);
        }

        [TestMethod]
        public async Task HandleAsync_WhenProcessingExpired_MarksFailedWithoutReplayingImport()
        {
            var importLogService = new FakeImportLogService
            {
                ImportLog = new FormDataImportLog
                {
                    Id = "log-1",
                    CorpId = "corp-1",
                    RetryCount = 0,
                    Status = FormDataImportStatus.Processing,
                    ProcessingExpireTime = DateTime.UtcNow.AddMinutes(-1).ToTimeStampMs(),
                },
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConnectionFactory, ConnectionFactory>();
            services.AddSingleton<IMessageRouteResolver, FakeMessageRouteResolver>();
            services.AddSingleton<IFormDataImportLogService>(importLogService);
            services.AddScoped<IResolver, TestResolver>();

            await using var provider = services.BuildServiceProvider();
            var consumer = new TestableDataImportConsumer(provider.GetRequiredService<IServiceScopeFactory>());

            await consumer.InvokeAsync(new DataImportTaskArgs
            {
                ImportLogId = "log-1",
                CorpId = "corp-1",
                RetryCount = 0,
            }, provider.GetRequiredService<IResolver>());

            Assert.AreEqual(1, importLogService.MarkFailedCalls);
            Assert.AreEqual(0, importLogService.TryMarkProcessingCalls);
        }

        private sealed class TestableDataImportConsumer(IServiceScopeFactory scopeFactory)
            : DataImportConsumer(scopeFactory)
        {
            public Task InvokeAsync(DataImportTaskArgs args, IResolver resolver)
            {
                return HandleAsync(args, CancellationToken.None, resolver);
            }

            public Task InvokeAsync(DataImportTaskArgs args, IResolver resolver, CancellationToken cancellationToken)
            {
                return HandleAsync(args, cancellationToken, resolver);
            }
        }

        private sealed class FakeImportLogService : IFormDataImportLogService
        {
            public FormDataImportLog? ImportLog { get; set; }

            public bool MarkProcessingResult { get; set; }

            public Exception? MarkProcessingException { get; set; }

            public int TryMarkProcessingCalls { get; private set; }

            public int MarkFailedCalls { get; private set; }

            public IMongoCollection<FormDataImportLog> Collection => throw new NotSupportedException();

            public FormDataImportLog? Get(string id) => ImportLog?.Id == id ? ImportLog : null;

            public Task<bool> TryMarkProcessingAsync(string id, int retryCount)
            {
                TryMarkProcessingCalls++;
                if (MarkProcessingException != null)
                {
                    throw MarkProcessingException;
                }

                return Task.FromResult(MarkProcessingResult);
            }

            public Task MarkFailedAsync(
                string id,
                string errorMessage,
                string? errorReportFileName = null,
                string? errorReportObjectKey = null,
                string? errorReportDownloadUrl = null)
            {
                MarkFailedCalls++;
                return Task.CompletedTask;
            }

            public Task MarkProcessingAsync(string id, long totalCount) => throw new NotSupportedException();
            public Task UpdateProgressAsync(string id, long processedCount, long addCount, long updateCount, long failedCount) => throw new NotSupportedException();
            public Task MarkSucceededAsync(string id, long totalCount, long addCount, long updateCount) => throw new NotSupportedException();
            public Task MarkCompletedWithErrorsAsync(string id, long totalCount, long addCount, long updateCount, long failedCount, string errorReportFileName, string errorReportObjectKey, string errorReportDownloadUrl, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount) => throw new NotSupportedException();
            public Task MarkCorrectionResultAsync(string id, long totalCount, long addCount, long updateCount, long failedCount, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount) => throw new NotSupportedException();
            public Task UpdateEditableErrorsAsync(string id, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount) => throw new NotSupportedException();
            public Task IncrementRetryAsync(string id) => throw new NotSupportedException();
            public IQueryable<FormDataImportLog> All() => throw new NotSupportedException();
            public IQueryable<FormDataImportLog> Query(Expression<Func<FormDataImportLog, bool>> where) => throw new NotSupportedException();
            public IFindFluent<FormDataImportLog, FormDataImportLog> Find(DynamicFindOptions<FormDataImportLog> options) => throw new NotSupportedException();
            public IFindFluent<FormDataImportLog, FormDataImportLog> Find(Expression<Func<FormDataImportLog, bool>> filter) => throw new NotSupportedException();
            public long Count(DynamicFilter filter) => throw new NotSupportedException();
            public long Count(Expression<Func<FormDataImportLog, bool>> filter) => throw new NotSupportedException();
            public bool Exists(Expression<Func<FormDataImportLog, bool>> where) => throw new NotSupportedException();
            public bool Exists(DynamicFilter where) => throw new NotSupportedException();
            public void Add(FormDataImportLog entity) => throw new NotSupportedException();
            public void Add(IEnumerable<FormDataImportLog> entities) => throw new NotSupportedException();
            public ReplaceOneResult Replace(FormDataImportLog entity) => throw new NotSupportedException();
            public object Delete(string id) => throw new NotSupportedException();
            public object Delete(IEnumerable<string> ids) => throw new NotSupportedException();
            public object Delete(DynamicFilter filter) => throw new NotSupportedException();
            public Task<FormDataImportLog?> GetAsync(string id) => throw new NotSupportedException();
            public Task<IAsyncCursor<FormDataImportLog>> FindAsync(DynamicFindOptions<FormDataImportLog> options) => throw new NotSupportedException();
            public Task<IAsyncCursor<FormDataImportLog>> FindAsync(Expression<Func<FormDataImportLog, bool>> filter) => throw new NotSupportedException();
            public Task<long> CountAsync(DynamicFilter filter) => throw new NotSupportedException();
            public Task<long> CountAsync(Expression<Func<FormDataImportLog, bool>> filter) => throw new NotSupportedException();
            public Task<bool> ExistsAsync(Expression<Func<FormDataImportLog, bool>> where) => throw new NotSupportedException();
            public Task<bool> ExistsAsync(DynamicFilter where) => throw new NotSupportedException();
            public Task AddAsync(FormDataImportLog entity) => throw new NotSupportedException();
            public Task AddAsync(IEnumerable<FormDataImportLog> entities) => throw new NotSupportedException();
            public Task<ReplaceOneResult> ReplaceAsync(FormDataImportLog entity) => throw new NotSupportedException();
            public Task<object> DeleteAsync(string id) => throw new NotSupportedException();
            public Task<object> DeleteAsync(IEnumerable<string> ids) => throw new NotSupportedException();
            public Task<object> DeleteAsync(DynamicFilter filter) => throw new NotSupportedException();
        }

        private sealed class FakeMessageRouteResolver : IMessageRouteResolver
        {
            public string ResolveQueueName(Type messageType) => "data-import";
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
