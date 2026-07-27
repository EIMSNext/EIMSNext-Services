using System.Security.Cryptography.X509Certificates;
using EIMSNext.ApiCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EIMSNext.Auth.Host;

internal sealed class ConfigureJwtBearerOptions(IConfiguration configuration, IWebHostEnvironment environment) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options)
    {
        Configure(JwtBearerDefaults.AuthenticationScheme, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var certificatePath = Path.Combine(environment.ContentRootPath, configuration.GetSection("Certificates:CerPath").Value!);
        var certificatePassword = configuration.GetSection("Certificates:Password").Value;
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            certificatePath,
            certificatePassword,
            X509KeyStorageFlags.DefaultKeySet);

        var oauthSection = configuration.GetSection("OAuth");
        var authority = oauthSection["Authority"];
        var issuer = oauthSection["Issuer"] ?? "https://auth.eimsnext.com";
        var audience = oauthSection["Audience"] ?? "eimsnext.api";

        options.Authority = null;
        options.Events = JwtBearerLogoutTokenEvents.Create();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new X509SecurityKey(certificate)
        };
    }

}
