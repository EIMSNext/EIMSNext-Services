using System.Security.Cryptography.X509Certificates;

using EIMSNext.Identity.AccountSecurity;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using EIMSNext.Identity.Persistence;
using EIMSNext.Identity.Services;
using EIMSNext.Identity.Utilities;
using EIMSNext.DingTalk.Clients;
using EIMSNext.Feishu.Clients;
using EIMSNext.WeChat.Clients;
using EIMSNext.WxWork.Clients;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenIddict.Abstractions;
using OpenIddict.Server;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Identity.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
        {
            services.Configure<PublicAccessOptions>(configuration.GetSection(PublicAccessOptions.SectionName));
            services.AddOptions<IdentityLoginAuditQueueOptions>()
                .Bind(configuration.GetSection(IdentityLoginAuditQueueOptions.SectionName))
                .Validate(options => options.Capacity > 0, "IdentityLoginAuditQueue:Capacity must be greater than zero.")
                .Validate(options => options.BatchSize > 0, "IdentityLoginAuditQueue:BatchSize must be greater than zero.")
                .Validate(options => options.FlushIntervalMs >= 10, "IdentityLoginAuditQueue:FlushIntervalMs must be at least 10.")
                .Validate(options => options.ShutdownDrainSeconds > 0, "IdentityLoginAuditQueue:ShutdownDrainSeconds must be greater than zero.")
                .ValidateOnStart();
            services.AddSingleton<IIdentityDbContext, IdentityDbContext>();
            services.AddSingleton<IdentityLoginAuditQueue>();
            services.AddHostedService<IdentityLoginAuditWriterService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPublicTokenService, PublicTokenService>();
            services.AddScoped<PublicSettingLookupService>();
            services.AddSingleton<IVerificationCodeProvider, MockVerificationCodeProvider>();
            services.AddScoped<IVerificationCodeService, VerificationCodeService>();
            services.AddScoped<ISingleSignOnService, SingleSignOnService>();
            services.AddScoped<IIntegrationAuthService, IntegrationAuthService>();
            services.AddScoped<IIntegrationProviderResolver, IntegrationProviderResolver>();
            services.AddScoped<IIdentityLoginAuditService, IdentityLoginAuditService>();
            services.AddSingleton<WeChatOpenClient>();
            services.AddSingleton<WxWorkClient>();
            services.AddSingleton<DingTalkClient>();
            services.AddSingleton<FeishuClient>();
            services.AddScoped<ITokenGrantHandler, PasswordTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, VerificationCodeTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, SingleSignOnTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, IntegrationTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, PublicTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, SystemTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, ClientCredentialsTokenGrantHandler>();
            services.AddScoped<ITokenRequestHandler, TokenRequestHandler>();

            var certificatePath = Path.Combine(contentRootPath, configuration.GetSection("Certificates:CerPath").Value!);
            var certificatePassword = configuration.GetSection("Certificates:Password").Value;
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                certificatePassword,
                X509KeyStorageFlags.EphemeralKeySet);

            services.AddOpenIddict()
                .AddServer(options =>
                {
                    var issuerValue = configuration.GetSection("OAuth:Issuer").Value
                        ?? "https://identity.eimsnext.com/issuer";
                    options.SetIssuer(issuerValue);
                    options.SetTokenEndpointUris("connect/token", "identity/login", "public/token", "system/token");
                    options.RegisterScopes(
                        Scopes.OpenId,
                        Scopes.Profile,
                        "api.readwrite",
                        nameof(EIMSNext.ApiService.PublicScope.DashLink),
                        nameof(EIMSNext.ApiService.PublicScope.FormLink),
                        nameof(EIMSNext.ApiService.PublicScope.DataLink),
                        nameof(EIMSNext.ApiService.PublicScope.QueryLink));

                    options.AllowPasswordFlow();
                    options.AllowCustomFlow(EIMSNext.Entities.CustomGrantType.VerificationCode);
                    options.AllowCustomFlow(EIMSNext.Entities.CustomGrantType.SingleSignOn);
                    options.AllowCustomFlow(EIMSNext.Entities.CustomGrantType.Integration);
                    options.AllowCustomFlow(EIMSNext.Entities.CustomGrantType.Public);
                    options.AllowCustomFlow(EIMSNext.Entities.CustomGrantType.System);
                    options.AllowCustomFlow(EIMSNext.Entities.CustomGrantType.ClientCredentials);

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
                    options.AddEventHandler<OpenIddictServerEvents.ExtractTokenRequestContext>(builder =>
                    {
                        builder.SetOrder(int.MaxValue);
                        builder.UseInlineHandler(context =>
                        {
                            var path = context.RequestUri?.AbsolutePath;
                            if (!string.Equals(path, "/identity/login", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(path, "/public/token", StringComparison.OrdinalIgnoreCase))
                            {
                                return default;
                            }

                            var encrypted = context.Request?.GetParameter("encrypted")?.ToString();
                            var parsed = TokenRequestHelper.ParseEncryptedFields(encrypted);
                            if (!parsed.Succeeded)
                            {
                                context.Reject(parsed.Error!, parsed.ErrorDescription!, null);
                                return default;
                            }

                            var fields = parsed.Fields!;
                            if (string.Equals(path, "/identity/login", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!fields.TryGetValue("grant_type", out var grantType) || string.IsNullOrWhiteSpace(grantType))
                                {
                                    fields["grant_type"] = GrantTypes.Password;
                                }
                            }
                            else
                            {
                                fields["grant_type"] = EIMSNext.Entities.CustomGrantType.Public;
                            }

                            context.Request = TokenRequestHelper.CreateRequest(fields.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)));
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
