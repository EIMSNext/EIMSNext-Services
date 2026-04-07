using EIMSNext.Async.Core.Jobs;
using EIMSNext.Async.Core.Messaging;
using EIMSNext.Async.Core.Messaging.Consumers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            services.AddSingleton<IHostedService, FormNotifyDispatchConsumer>();
            services.AddSingleton<IHostedService, SystemMessageConsumer>();
            services.AddSingleton<IHostedService, EmailConsumer>();

            return services;
        }
    }
}
