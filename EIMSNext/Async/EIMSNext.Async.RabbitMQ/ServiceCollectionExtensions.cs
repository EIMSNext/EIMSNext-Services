using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using RabbitMQ.Client;

namespace EIMSNext.Async.RabbitMQ
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IConnection>(_ =>
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                    UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                    Password = configuration["RabbitMQ:Password"] ?? "guest"
                };

                return factory.CreateConnection();
            });

            services.AddSingleton<IMessageRouteResolver, AttributeMessageRouteResolver>();
            services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();

            return services;
        }
    }
}
