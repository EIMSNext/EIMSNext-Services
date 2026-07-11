using System.Security.Cryptography.X509Certificates;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.Auth.Persistence;
using EIMSNext.Auth.Services;
using EIMSNext.Auth.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Auth.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
        {
            services.Configure<PublicAccessOptions>(configuration.GetSection(PublicAccessOptions.SectionName));
            services.AddScoped<IAuthDbContext, AuthDbContext>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPublicTokenService, PublicTokenService>();
            services.AddScoped<PublicSettingLookupService>();
            services.AddScoped<IVerificationCodeService, VerificationCodeService>();
            services.AddScoped<ISingleSignOnService, SingleSignOnService>();
            services.AddScoped<IAuditLoginService, AuditLoginService>();
            services.AddScoped<ITokenGrantHandler, PasswordTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, VerificationCodeTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, SingleSignOnTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, PublicTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, SystemTaskTokenGrantHandler>();
            services.AddScoped<ITokenGrantHandler, ClientCredentialsTokenGrantHandler>();
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
                        ?? "https://auth.eimsnext.com";
                    options.SetIssuer(new Uri(issuerValue));
                    options.SetTokenEndpointUris("connect/token", "auth/login", "public/token", "system/token");
                    options.RegisterScopes(
                        Scopes.OpenId,
                        Scopes.Profile,
                        "api.readwrite",
                        nameof(EIMSNext.ApiService.PublicScope.DashLink),
                        nameof(EIMSNext.ApiService.PublicScope.FormLink),
                        nameof(EIMSNext.ApiService.PublicScope.DataLink),
                        nameof(EIMSNext.ApiService.PublicScope.QueryLink));

                    options.AllowPasswordFlow();
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.VerificationCode);
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.SingleSignOn);
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.Public);
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.System);
                    options.AllowCustomFlow(EIMSNext.Auth.Entities.CustomGrantType.ClientCredentials);

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
                            if (!string.Equals(path, "/auth/login", StringComparison.OrdinalIgnoreCase) &&
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
                            if (string.Equals(path, "/auth/login", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!fields.TryGetValue("grant_type", out var grantType) || string.IsNullOrWhiteSpace(grantType))
                                {
                                    fields["grant_type"] = GrantTypes.Password;
                                }
                            }
                            else
                            {
                                fields["grant_type"] = EIMSNext.Auth.Entities.CustomGrantType.Public;
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
