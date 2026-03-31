using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Async.Core.Interfaces;

using Microsoft.Extensions.Logging;

using Quartz;

namespace EIMSNext.Async.Core.Jobs
{
    /// <summary>
    /// 每日测试作业（Quartz 作业实现）
    /// </summary>
    [DisallowConcurrentExecution] // 防止并发执行
    public class TestJob : IJob, ITestJob
    {
        private readonly ILogger<TestJob> _logger;
        private readonly IDateTimeProvider _dateTimeProvider;

        // Quartz 通过 DI 解析此构造函数
        public TestJob(ILogger<TestJob> logger, IDateTimeProvider dateTimeProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        }

        /// <summary>
        /// Quartz 调度入口
        /// </summary>
        public Task Execute(IJobExecutionContext context)
            => ExecuteAsync(); // 委托给业务方法

        /// <summary>
        /// 业务逻辑实现（便于单元测试）
        /// </summary>
        public async Task ExecuteAsync()
        {
            var startTime = _dateTimeProvider.Now;
            _logger.LogInformation(
                "🚀 [TestJob] Started | Server: {Server} | Time: {StartTime:yyyy-MM-dd HH:mm:ss}",
                Environment.MachineName,
                startTime);

            try
            {
                // 模拟业务处理（异步友好）
                await Task.Delay(800);

                _logger.LogInformation(
                    "✅ [TestJob] Completed successfully | Duration: {Duration}ms | Result: ValidationPassed",
                    (int)(_dateTimeProvider.Now - startTime).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [TestJob] Failed: {ErrorMessage}", ex.Message);
                throw;
            }
        }
    }
}
