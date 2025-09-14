using IdentityServer4.Services;
using IdentityServer4.Stores;

using EIMSNext.Auth;
using EIMSNext.Auth.DbContext;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Options;
using EIMSNext.Auth.Services;
using EIMSNext.Auth.Stores;
using EIMSNext.MongoDb;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MongoDbBuilderExtensions
    {
        public static IIdentityServerBuilder AddMongoDbStore(this IIdentityServerBuilder builder, IConfiguration configuration)
        {
            builder.Services.Configure<MongoDbConfiguration>(configuration);
            builder.Services.AddScoped<IAuthDbContext, AuthDbContext>();

            AddConfigurationStore(builder);
            AddOperationalStore(builder, (tco) =>
            {
                tco.Enable = true;
                tco.Interval = 3600;
            });

            return builder;
        }

        private static IIdentityServerBuilder AddConfigurationStore(
            IIdentityServerBuilder builder)
        {
            builder.Services.AddScoped<IClientStore, ClientStore>();
            builder.Services.AddScoped<IResourceStore, ResourceStore>();
            builder.Services.AddScoped<ICorsPolicyService, CorsPolicyService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IVerificationCodeService, VerificationCodeService>();
            builder.Services.AddScoped<ISingleSignOnService, SingleSignOnService>();

            return builder;
        }

        private static IIdentityServerBuilder AddOperationalStore(
            this IIdentityServerBuilder builder,
            Action<TokenCleanupOptions>? tokenCleanUpOptions = null)
        {
            builder.Services.AddTransient<IPersistedGrantStore, PersistedGrantStore>();

            var tco = new TokenCleanupOptions();
            tokenCleanUpOptions?.Invoke(tco);
            builder.Services.AddSingleton(tco);
            builder.Services.AddTransient<TokenCleanup>();
            builder.Services.AddSingleton<IHostedService, TokenCleanupService>();

            return builder;
        }
    }
}