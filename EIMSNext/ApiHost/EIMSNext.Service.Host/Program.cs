using Asp.Versioning;
using EIMSNext.ApiCore;
using EIMSNext.ApiCore.Plugin;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.Async.RabbitMQ;
using EIMSNext.Component;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Plugin.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Extensions;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Formatter.Deserialization;
using Microsoft.AspNetCore.OData.Formatter.Serialization;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigWebEnvironment();
builder.Services.AddServiceComponents();

builder.Host.UseAutofac<AutofacRegisterModule>();

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "EIMSNext.Service.Host"));
// Add services to the container.
builder.Services.AddControllers().AddOData(
         options =>
         {
             options.TimeZone = TimeZoneInfo.Utc;
             options.EnableQueryFeatures(EIMSNext.Common.Constants.MaxPageSize)
             //移除$metadata访问
             .Conventions.Remove(options.Conventions.OfType<MetadataRoutingConvention>().First());

             options.RouteOptions.EnableControllerNameCaseInsensitive = true;
             options.RouteOptions.EnableActionNameCaseInsensitive = true;
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

builder.Services.AddGlobalMef(EIMSNext.Common.Constants.BaseDirectory);
builder.Services.AddPluginRuntime(EIMSNext.Common.Constants.BaseDirectory);
builder.Services.AddRabbitMqMessaging(builder.Configuration);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddTransient<ISwaggerGenHandler, SwaggerGenHandler>();
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

app.UseSerilogRequestLogging();
app.UseCustomMiddlewares();
app.UseMiddleware<ODataMetadataMiddleware>();
app.UseMiddleware<ODataCountRequestMiddleware>();
app.UseODataQueryRequest();
//app.UseODataBatching();
//app.UseHttpsRedirection();

//app.UseStaticFiles(new StaticFileOptions()
//{
//    OnPrepareResponse = (e) =>
//    {
//        e.Context.Response.Headers.AccessControlAllowOrigin = e.Context.Request.Headers.Origin;
//        e.Context.Response.Headers.AccessControlAllowMethods = "PUT,POST,GET,DELETE,OPTIONS,HEAD,PATCH";
//        e.Context.Response.Headers.AccessControlAllowHeaders = e.Context.Request.Headers.AccessControlRequestHeaders;
//        e.Context.Response.Headers.AccessControlAllowCredentials = "true";
//    }
//});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

async void EnsureSeedData(IResolver resolver)
{
    resolver.GetServiceContext().Operator = new Operator("", "", "");
    var corpService = resolver.GetService<Corporate>();
    var pluginProfileRepo = resolver.GetRepository<PluginProfile>();
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

    if (!pluginProfileRepo.Queryable.Any(x => x.PluginId == "sampleplugin" && !x.DeleteFlag))
    {
        var profile = new PluginProfile
        {
            Id = pluginProfileRepo.NewId(),
            PluginId = "sampleplugin",
            Version = "1.0",
            Name = "示例插件",
            Summary = "演示插件市场、插件详情和函数清单展示。",
            Description = "示例收款单插件，可用于验证插件市场接入、安装管理和函数展示。",
            Category = "表单增强",
            Scenario = "信息查询",
            DeveloperName = "EIMSNext Team",
            IsOfficial = true,
            IsRecommended = true,
            Status = "Published",
            SortIndex = 1000,
            InstallCount = 0,
            PublishedAt = DateTime.UtcNow,
            HelpDocUrl = string.Empty,
            TemplateUrl = string.Empty,
            PricingPlans =
            [
                new PluginPricingPlan
                {
                    Id = "free",
                    Name = "免费试用",
                    Price = 0,
                    DurationDays = 30,
                    Unit = "天",
                    IsTrial = true
                }
            ],
            Functions =
            [
                new PluginFunctionSnapshot
                {
                    Id = "EchoReceipt",
                    Name = "收款单回显",
                    Description = "演示插件字段映射、执行结果开放字段与下游节点联动",
                    InputFields =
                    [
                        new PluginFieldDesc { Key = "bizNo", Name = "单据编号", FieldType = "Input", Required = true },
                        new PluginFieldDesc { Key = "amount", Name = "金额", FieldType = "Number", Required = true },
                        new PluginFieldDesc { Key = "bizDate", Name = "业务日期", FieldType = "TimeStamp" },
                        new PluginFieldDesc { Key = "remark", Name = "备注", FieldType = "TextArea" },
                        new PluginFieldDesc { Key = "items", Name = "明细子表", FieldType = "TableForm" }
                    ]
                },
                new PluginFunctionSnapshot
                {
                    Id = "EchoMixedData",
                    Name = "通用字段回显",
                    Description = "用于验证插件切换方法、字段重置和结果字段选择",
                    InputFields =
                    [
                        new PluginFieldDesc { Key = "title", Name = "标题", FieldType = "Input", Required = true },
                        new PluginFieldDesc { Key = "description", Name = "描述", FieldType = "TextArea" },
                        new PluginFieldDesc { Key = "owner", Name = "负责人", FieldType = "Employee1" },
                        new PluginFieldDesc { Key = "ownerDept", Name = "归属部门", FieldType = "Department1" }
                    ]
                }
            ]
        };
        await pluginProfileRepo.InsertAsync(profile);
    }
}
