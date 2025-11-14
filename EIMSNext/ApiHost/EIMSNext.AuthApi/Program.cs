using System.Security.Cryptography.X509Certificates;
using EIMSNext.ApiCore;
using EIMSNext.Auth.GrantValidator;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Mappers;
using EIMSNext.Auth.Services;
using EIMSNext.AuthApi;
using EIMSNext.Core;
using Microsoft.OpenApi.Models;
using NLog.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigCommonServices();
builder.Services.AddLogging(c => { c.AddNLog("nlog.config"); });

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo() { Title = "EIMSNext.Auth", Version = "v1" });
});

builder.Services.AddIdentityServer(options =>
    {
        options.Events.RaiseSuccessEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseErrorEvents = true;
        options.IssuerUri = "https://auth.eimsnext.com";
    })
  .AddMongoDbStore(builder.Configuration.GetSection("MongoDB"))
  .AddSigningCredential(new X509Certificate2(
      Path.Combine(builder.Environment.ContentRootPath, builder.Configuration.GetSection("Certificates:CerPath").Value!),
      builder.Configuration.GetSection("Certificates:Password").Value))
  .AddExtensionGrantValidator<VerificationCodeGrantValidator>()
  .AddExtensionGrantValidator<SingleSignOnGrantValidator>()
  .AddExtensionGrantValidator<IntegrationGrantValidator>()
  .AddResourceOwnerValidator<ResourceUserPasswordValidator>()
  .AddProfileService<UserProfileService>()
  .AddJwtBearerClientAuthentication();

//.AddAppAuthRedirectUriValidator()              

//builder.Services.AddScoped<IResolver, DefaultResolver>();
//var mefContainer = new ContainerConfiguration();
//builder.Services.EnableMef2(mefContainer);

var app = builder.Build();

// Setup Databases
using (var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
{
    EnsureSeedData(serviceScope.ServiceProvider.GetService<IAuthDbContext>()!);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCustomMiddlewares();

app.UseIdentityServer();
//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();


void EnsureSeedData(IAuthDbContext context)
{
    if (!context.Clients.Any())
    {
        foreach (var client in SeedData.GetClients().ToList())
        {
            context.AddClient(client.ToEntity());
        }
    }

    if (!context.IdentityResources.Any())
    {
        foreach (var resource in SeedData.GetIdentityResources())
        {
            context.AddIdentityResource(resource.ToEntity());
        }
    }

    if (!context.ApiResources.Any())
    {
        foreach (var resource in SeedData.GetApiResources())
        {
            context.AddApiResource(resource.ToEntity());
        }
    }

    if (!context.ApiScopes.Any())
    {
        foreach (var resource in SeedData.GetApiScopes())
        {
            context.AddApiScope(resource.ToEntity());
        }
    }
    if (!context.Users.Any())
    {
        foreach (var user in SeedData.GetUsers())
        {
            context.AddUser(user);
        }
    }
}

