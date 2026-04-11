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
            // Register a ConnectionFactory and delay actual connection creation to runtime
            services.AddSingleton<IConnectionFactory>(_ =>
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                    UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                    Password = configuration["RabbitMQ:Password"] ?? "guest"
                };
                return factory;
            });

            // Note: We do not create a connection at startup to avoid blocking startup if MQ is unavailable.
            // Consumers/publishers will create connections on demand from the IConnectionFactory.

            services.AddSingleton<IMessageRouteResolver, AttributeMessageRouteResolver>();
            services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();

            return services;
        }
    }
}
