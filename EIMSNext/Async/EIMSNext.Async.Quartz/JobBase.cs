using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace EIMSNext.Async.Quartz
{
    public abstract class JobBase<T> : IJob where T : IJob
    {
        protected IResolver Resolver { get; }

        protected ILogger<T> Logger { get; }

        protected JobBase(IResolver resolver)
        {
            Resolver = resolver;
            Logger = resolver.Resolve<ILogger<T>>();
        }
        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                return ExecuteAsync(context);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"{nameof(T)} Execute Fail.");
                return Task.FromException(ex);
            }
        }

        protected abstract Task ExecuteAsync(IJobExecutionContext context);
    }
}
