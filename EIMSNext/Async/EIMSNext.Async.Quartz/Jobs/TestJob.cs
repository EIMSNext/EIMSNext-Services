using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;

using Quartz;

namespace EIMSNext.Async.Quartz.Jobs
{
    [DisallowConcurrentExecution]
    public class TestJob : JobBase<TestJob>, IJob, ITestJob
    {
        public TestJob(IResolver resolver) : base(resolver) { }


        protected override async Task ExecuteAsync(IJobExecutionContext context)
        {
            await Task.Delay(800);
        }
    }
}
