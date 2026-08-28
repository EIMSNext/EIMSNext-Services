using EIMSNext.ApiCore;
using EIMSNext.Mef;
using EIMSNext.Identity.Extensions;
using EIMSNext.Identity.Host;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Services;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigWebEnvironment();
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "EIMSNext.Identity.Host"));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
});
builder.Services.AddAuthorization();
builder.Services.Configure<BuiltInClientsOptions>(builder.Configuration.GetSection(BuiltInClientsOptions.SectionName));
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
builder.Services.AddSingleton<IBuiltInClientRequestPolicy, BuiltInClientRequestPolicy>();
builder.Services.AddScoped<IAccountSecurityService, AccountSecurityService>();
builder.Services.AddGlobalMef(EIMSNext.Common.Constants.BaseDirectory);
builder.Services.AddIdentityServices(builder.Configuration, builder.Environment.ContentRootPath);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo() { Title = "EIMSNext.Identity", Version = "v1" });
});

//.AddAppAuthRedirectUriValidator()

//builder.Services.AddScoped<IResolver, DefaultResolver>();
//var mefContainer = new ContainerConfiguration();
//builder.Services.EnableMef2(mefContainer);

var app = builder.Build();

// Setup Databases
using (var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
{
    EnsureSeedData(serviceScope.ServiceProvider.GetService<IIdentityDbContext>()!, app.Configuration);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCustomMiddlewares();

//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();


void EnsureSeedData(IIdentityDbContext context, IConfiguration configuration)
{
    var seedClients = SeedData.GetClients(configuration).ToList();
    if (!context.Clients.Any())
    {
        foreach (var client in seedClients)
        {
            context.AddClient(client);
        }
    }
    else
    {
        foreach (var seedClient in seedClients)
        {
            var client = context.Clients.FirstOrDefault(x => x.Id == seedClient.Id);
            if (client == null)
            {
                context.AddClient(seedClient).GetAwaiter().GetResult();
                continue;
            }

            var changed = false;
            if (!string.Equals(client.Name, seedClient.Name, StringComparison.Ordinal))
            {
                client.Name = seedClient.Name;
                changed = true;
            }

            if (client.RequireClientSecret != seedClient.RequireClientSecret)
            {
                client.RequireClientSecret = seedClient.RequireClientSecret;
                changed = true;
            }

            var currentGrantTypes = client.AllowedGrantTypes
                .Select(x => x.GrantType)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            var seedGrantTypes = seedClient.AllowedGrantTypes
                .Select(x => x.GrantType)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (!currentGrantTypes.SequenceEqual(seedGrantTypes, StringComparer.Ordinal))
            {
                client.AllowedGrantTypes = seedClient.AllowedGrantTypes
                    .Select(x => new EIMSNext.Entities.ClientGrantType { GrantType = x.GrantType })
                    .ToList();
                changed = true;
            }

            var currentScopes = client.AllowedScopes
                .Select(x => x.Scope)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            var seedScopes = seedClient.AllowedScopes
                .Select(x => x.Scope)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (!currentScopes.SequenceEqual(seedScopes, StringComparer.Ordinal))
            {
                client.AllowedScopes = seedClient.AllowedScopes
                    .Select(x => new EIMSNext.Entities.ClientScope { Scope = x.Scope })
                    .ToList();
                changed = true;
            }

            if (changed)
            {
                context.UpdateClient(client).GetAwaiter().GetResult();
            }
        }
    }

    if (!context.Users.Any())
    {
        foreach (var user in SeedData.GetUsers())
        {
            context.AddUser(user);
        }
    }

    if (!context.IntegrationLoginSettings.Any())
    {
        foreach (var setting in SeedData.GetIntegrationLoginSettings())
        {
            context.AddIntegrationLoginSetting(setting);
        }
    }
}
