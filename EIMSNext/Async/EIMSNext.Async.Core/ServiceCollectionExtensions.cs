using EIMSNext.Async.Core.Interface;
using EIMSNext.Async.Core.Jobs;
using EIMSNext.Async.Core.MQ;
using EIMSNext.Async.Core.MQ.Consumers;

using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Async.Core
{
    /// <summary>
    /// 业务服务注册扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 注册所有业务作业及相关服务
        /// </summary>
        public static IServiceCollection AddJobs(this IServiceCollection services)
        {
            // 注册业务服务（Quartz 作业依赖）
            services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
            services.AddTransient<TestJob>(); // Quartz 通过 DI 解析作业

            // 注册业务接口（便于测试/替换）
            services.AddTransient<ITestJob>(sp => sp.GetRequiredService<TestJob>());

            return services;
        }

        /// <summary>
        /// 注册RabbitMQ消费者及相关服务
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddConsumers(this IServiceCollection services)
        {
            // ========== 业务服务注册（仅引用 Business 项目） ==========
            services.AddSingleton<TaskProducer>(); // 生产者供其他服务使用

            // ========== 消费者注册（自动发现 Attribute） ==========
            services.AddScoped<EmailConsumer>();

            return services;
        }
    }
}
