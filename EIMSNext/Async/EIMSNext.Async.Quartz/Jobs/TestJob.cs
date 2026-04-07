using Microsoft.Extensions.Logging;

using Quartz;

namespace EIMSNext.Async.Quartz.Jobs
{
    [DisallowConcurrentExecution]
    public class TestJob(ILogger<TestJob> logger) : IJob, ITestJob
    {
        private readonly ILogger<TestJob> _logger = logger;

        public Task Execute(IJobExecutionContext context)
            => ExecuteAsync();

        public async Task ExecuteAsync()
        {
            try
            {
                await Task.Delay(800);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TestJob] Failed: {ErrorMessage}", ex.Message);
                throw;
            }
        }
    }
}
