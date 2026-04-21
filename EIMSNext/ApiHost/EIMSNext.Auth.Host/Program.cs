using EIMSNext.ApiCore;
using EIMSNext.Auth.Extensions;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Services;
using EIMSNext.Auth.Host;
using EIMSNext.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigWebEnvironment();
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "EIMSNext.Auth.Host"));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureAuthHostJwtBearerOptions>();
builder.Services.AddScoped<IAccountSecurityService, AccountSecurityService>();
builder.Services.AddAuthServices(builder.Configuration, builder.Environment.ContentRootPath);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo() { Title = "EIMSNext.Auth", Version = "v1" });
});

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

app.UseSerilogRequestLogging();
app.UseCustomMiddlewares();

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
            context.AddClient(client);
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

