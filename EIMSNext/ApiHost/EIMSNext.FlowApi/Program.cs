using Asp.Versioning;

using EIMSNext.ApiCore;
using EIMSNext.ApiHost.Extension;
using EIMSNext.Component;
using EIMSNext.Flow.Core;
using EIMSNext.Flow.Core.Interface;
using EIMSNext.Flow.Persistence;
using EIMSNext.Flow.Service;
using EIMSNext.FlowApi.Extension;
using EIMSNext.MongoDb;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using NLog.Extensions.Logging;

using Swashbuckle.AspNetCore.SwaggerGen;

using WorkflowCore.Interface;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigCommonServices();
builder.Services.AddServiceComponents();

// Add services to the container.
builder.Host.UseAutofac<AutofacRegisterModule>();

builder.Services.AddLogging(c => { c.AddNLog("nlog.config"); });
builder.Services.AddHealthChecks().AddCheck("health", () => HealthCheckResult.Healthy());

builder.Services.AddControllers();

builder.Services.AddWorkflow(opt =>
{
    opt.UseMongoDB((services) => services.GetRequiredService<IMongoDbContex>().Database);
});

builder.Services.AddStepBodys();
builder.Services.AddWorkflowServices();

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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

var host = app.Services.GetRequiredService<IWorkflowHost>();
app.Services.GetRequiredService<IWorkflowLoader>().LoadDefinitionsFromStorage();
//host.RegisterWorkflow<DynamicWorkflow, WfDataContext>();
host.Start();
//app.r.Register(() =>
//{
//    host.Stop();
//    backplane.Stop();
//});

app.Run();


