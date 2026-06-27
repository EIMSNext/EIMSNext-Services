using System.Security.Cryptography.X509Certificates;
using EIMSNext.Auth.Integrations.Abstractions;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.Auth.Persistence;
using EIMSNext.Auth.Services;
using EIMSNext.DingTalk.Clients;
using EIMSNext.Feishu.Clients;
using EIMSNext.WeChat.Clients;
using EIMSNext.WxWork.Clients;
using EIMSNext.MongoDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace EIMSNext.Auth.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
        {
            services.Configure<PublicAccessOptions>(configuration.GetSection(PublicAccessOptions.SectionName));
            services.AddAuthMongoCollections(configuration);
            services.AddScoped<IAuthDbContext, AuthDbContext>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPublicTokenService, PublicTokenService>();
            services.AddScoped<PublicSettingLookupService>();
            services.AddScoped<IVerificationCodeService, VerificationCodeService>();
            services.AddScoped<ISingleSignOnService, SingleSignOnService>();
            services.AddScoped<IIntegrationAuthService, IntegrationAuthService>();
            services.AddScoped<IIntegrationProviderResolver, IntegrationProviderResolver>();
            services.AddScoped<IAuditLoginService, AuditLoginService>();
            services.AddSingleton<WeChatOpenClient>();
            services.AddSingleton<WxWorkClient>();
            services.AddSingleton<DingTalkClient>();
            services.AddSingleton<FeishuClient>();
            services.AddScoped<ITokenGrantHandler, PasswordTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, VerificationCodeTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, SingleSignOnTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, IntegrationTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, PublicTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, SystemTaskTokenGrantHandler>();
            services.AddScoped<ITokenRequestHandler, TokenRequestHandler>();

            var certificatePath = Path.Combine(contentRootPath, configuration.GetSection("Certificates:CerPath").Value!);
            var certificatePassword = configuration.GetSection("Certificates:Password").Value;
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                certificatePassword,
                X509KeyStorageFlags.DefaultKeySet);

            services.AddOpenIddict()
                .AddServer(options =>
                {
                    var issuerValue = configuration.GetSection("OAuth:Issuer").Value
                        ?? configuration.GetSection("OAuth:Authority").Value
                        ?? "https://auth.eimsnext.com";
                    options.SetIssuer(new Uri(issuerValue));
                    options.SetTokenEndpointUris("connect/token");

                    options.AllowPasswordFlow();
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.VerificationCode);
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.SingleSignOn);
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.Integration);
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.Public);
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.SystemTask);

                    options.EnableDegradedMode();
                    options.AcceptAnonymousClients();
                    options.IgnoreEndpointPermissions();
                    options.IgnoreGrantTypePermissions();
                    options.IgnoreScopePermissions();
                    options.DisableAccessTokenEncryption();

                    options.AddEncryptionCertificate(certificate);
                    options.AddSigningCertificate(certificate);
                    options.AddEventHandler<OpenIddictServerEvents.ValidateTokenRequestContext>(builder =>
                    {
                        builder.UseInlineHandler(context =>
                        {
                            return default;
                        });
                    });
                    options.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder =>
                    {
                        builder.UseInlineHandler(context =>
                        {
                            context.IncludeIdentityToken = false;

                            if (context.Properties?.TryGetValue("access_token_lifetime", out var value) == true &&
                                int.TryParse(value, out var lifetime) &&
                                lifetime > 0)
                            {
                                var createdAt = DateTimeOffset.UtcNow;
                                var expiresAt = createdAt.AddSeconds(lifetime);

                                context.AccessTokenPrincipal?.SetCreationDate(createdAt);
                                context.AccessTokenPrincipal?.SetExpirationDate(expiresAt);
                                context.AccessTokenPrincipal?.SetAccessTokenLifetime(TimeSpan.FromSeconds(lifetime));
                            }

                            return default;
                        });
                    });
                    options.AddEventHandler<OpenIddictServerEvents.ApplyTokenResponseContext>(builder =>
                    {
                        builder.UseInlineHandler(context =>
                        {
                            if (context.Response is null)
                            {
                                return default;
                            }

                            context.Response.IdToken = null;

                            return default;
                        });
                    });

                    options.UseAspNetCore()
                        .DisableTransportSecurityRequirement()
                        .EnableTokenEndpointPassthrough();
                });

            return services;
        }
    }
}
