using Asp.Versioning;

using EIMSNext.ApiCore;
using EIMSNext.ApiHost.Extension;
using EIMSNext.Component;
using EIMSNext.FileUpload;
using EIMSNext.FileUploadApi.Extension;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using NLog.Extensions.Logging;

using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigCommonServices();

// Add services to the container.
builder.Host.UseAutofac<AutofacRegisterModule>();

builder.Services.AddLogging(c => { c.AddNLog("nlog.config"); });
builder.Services.AddHealthChecks().AddCheck("health", () => HealthCheckResult.Healthy());

builder.Services.AddControllers();

builder.Services.AddUploadedServices();

builder.Services.AddApiVersioning(opt =>
{
    opt.DefaultApiVersion = new ApiVersion(1.0);
    opt.AssumeDefaultVersionWhenUnspecified = true;
    opt.ReportApiVersions = true;

    opt.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddMvc().AddApiExplorer(opt =>
{
    opt.GroupNameFormat = "'v'VVV";
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddTransient<ISwaggerGenHandler, SwaggerGenHandler>();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, VersioningSwaggerGenOptions>();
builder.Services.AddSwaggerGen();

builder.Services.AddDefaultMef(EIMSNext.Common.Constants.BaseDirectory, "*Plugin.dll");

var app = builder.Build();

app.UseCustomMiddlewares();

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


