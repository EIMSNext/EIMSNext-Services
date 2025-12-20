using Asp.Versioning;

using EIMSNext.ApiCore;
using EIMSNext.Component;
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;
using EIMSNext.ServiceApi.Extension;
using EIMSNext.ServiceApi.OData;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Formatter.Deserialization;
using Microsoft.AspNetCore.OData.Formatter.Serialization;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using NLog.Extensions.Logging;

using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigCommonServices();
builder.Services.AddServiceComponents();

builder.Host.UseAutofac<AutofacRegisterModule>();

builder.Services.AddLogging(c => { c.AddNLog("nlog.config"); });
// Add services to the container.
builder.Services.AddControllers().AddOData(
         options =>
         {
             options.TimeZone = TimeZoneInfo.Utc;
             options.EnableQueryFeatures(EIMSNext.Common.Constants.MaxPageSize)
             //移除$metadata访问
             .Conventions.Remove(options.Conventions.OfType<MetadataRoutingConvention>().First());

             options.RouteOptions.EnableControllerNameCaseInsensitive = true;
             options.RouteOptions.EnableActionNameCaseInsensitive=true;
             options.RouteOptions.EnablePropertyNameCaseInsensitive = true;
         }
    );

//builder.Services.AddSingleton<SkipTokenHandler, CustomSkipTokenHandler>();

builder.Services.AddHealthChecks().AddCheck("health", () => HealthCheckResult.Healthy());

builder.Services.AddApiVersioning(opt =>
{
    opt.DefaultApiVersion = new ApiVersion(1.0);
    opt.AssumeDefaultVersionWhenUnspecified = true;
    opt.ReportApiVersions = true;

    opt.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddMvc().AddApiExplorer(opt =>
{
    opt.GroupNameFormat = "'v'VVV";
})
    .AddOData(opt => opt.AddRouteComponents("odata/v{version:apiVersion}",
    (services) =>
    {
        services.AddSingleton<ODataEnumDeserializer, LowercaseODataEnumDeserializer>();
        services.AddSingleton<ODataEnumSerializer, LowercaseODataEnumSerializer>();
    })
    )
    .AddODataApiExplorer(opt =>
{
    opt.GroupNameFormat = "'v'VVV";
});

builder.Services.AddDefaultMef(EIMSNext.Common.Constants.BaseDirectory, "*Plugin.dll");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, VersioningSwaggerGenOptions>();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Setup Databases
using (var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
{
    EnsureSeedData(serviceScope.ServiceProvider.GetService<IResolver>()!);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (var description in app.DescribeApiVersions())
        {
            var url = $"{description.GroupName}/swagger.json";
            var name = description.GroupName.ToUpperInvariant();
            options.SwaggerEndpoint(url, name);
        }
    });
}

app.UseCustomMiddlewares();
app.UseMiddleware<ODataMetadataMiddleware>();
app.UseMiddleware<ODataCountRequestMiddleware>();
app.UseODataQueryRequest();
//app.UseODataBatching();
//app.UseHttpsRedirection();

app.UseStaticFiles(new StaticFileOptions()
{
    OnPrepareResponse = (e) =>
    {
        e.Context.Response.Headers.AccessControlAllowOrigin = e.Context.Request.Headers.Origin;
        e.Context.Response.Headers.AccessControlAllowMethods = "PUT,POST,GET,DELETE,OPTIONS,HEAD,PATCH";
        e.Context.Response.Headers.AccessControlAllowHeaders = e.Context.Request.Headers.AccessControlRequestHeaders;
        e.Context.Response.Headers.AccessControlAllowCredentials = "true";
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

async void EnsureSeedData(IResolver resolver)
{
    resolver.GetServiceContext().Operator = new Operator("", "admin", "", "");
    var corpService = resolver.GetService<Corporate>();
    if (!corpService!.All().Any())
    {
        await corpService.AddAsync(
              new Corporate
              {
                  Code = "2008080800008",
                  Name = "EIMS Team",
                  Description = "EIMS Team",
              });
    }
}
