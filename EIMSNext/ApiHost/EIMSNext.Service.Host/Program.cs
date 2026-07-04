using Asp.Versioning;
using EIMSNext.ApiCore;
using EIMSNext.ApiCore.Plugin;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.Auth.Entities;
using EIMSNext.Async.RabbitMQ;
using EIMSNext.Component;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.ApiService;
using EIMSNext.Plugin.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Authorization;
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
builder.Services.AddControllers(options =>
{
    options.Filters.Add<IdentityTypeFilter>();
    options.Filters.Add<PermissionFilter>();
}).AddOData(
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
builder.Services.AddScoped<IPublicAccessValidator, PublicAccessValidator>();
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
    await EnsureSeedData(serviceScope.ServiceProvider.GetRequiredService<IResolver>());
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

async Task EnsureSeedData(IResolver resolver)
{
    var serviceContext = resolver.GetServiceContext();
    serviceContext.UserId = "admin";
    serviceContext.Operator = new Operator("", "admin", "Admin");

    var corpService = resolver.GetService<Corporate>();
    var pluginProfileRepo = resolver.GetRepository<PluginProfile>();
    if (!corpService!.All().Any())
    {
        var userRepo = resolver.GetRepository<User>();
        var adminUser = userRepo.Queryable.FirstOrDefault(x => x.Id == "admin");
        if (adminUser == null && !userRepo.Queryable.Any())
        {
            adminUser = new User
            {
                Id = "admin",
                Name = "Admin",
                Password = HKH.Common.Security.BCrypt.HashPassword("123456"),
                Email = "admin@eimsnext.com",
                Phone = "12345678901"
            };
            await userRepo.InsertAsync(adminUser);
        }

        if (adminUser == null)
        {
            throw new InvalidOperationException("初始化企业需要 admin 用户，请先初始化 Auth 数据。");
        }

        serviceContext.User = adminUser;

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
                        new PluginFieldDesc { Key = "bizNo", Name = "单据编号", FieldType = PluginFieldKind.Text, Required = true },
                        new PluginFieldDesc { Key = "amount", Name = "金额", FieldType = PluginFieldKind.Number, Required = true },
                        new PluginFieldDesc { Key = "bizDate", Name = "业务日期", FieldType = PluginFieldKind.Timestamp },
                        new PluginFieldDesc { Key = "remark", Name = "备注", FieldType = PluginFieldKind.TextArea },
                        new PluginFieldDesc { Key = "status", Name = "状态", FieldType = PluginFieldKind.SingleSelect, CompatibleFieldTypes = { PluginFieldKind.Radio } },
                        new PluginFieldDesc { Key = "receiver", Name = "经办人", FieldType = PluginFieldKind.SingleEmployee },
                        new PluginFieldDesc { Key = "dept", Name = "部门", FieldType = PluginFieldKind.SingleDepartment },
                        new PluginFieldDesc { Key = "attachments", Name = "附件", FieldType = PluginFieldKind.FileUpload, Multiple = true },
                        new PluginFieldDesc { Key = "images", Name = "图片", FieldType = PluginFieldKind.ImageUpload, Multiple = true },
                        new PluginFieldDesc { Key = "items", Name = "明细子表", FieldType = PluginFieldKind.TableForm, Multiple = true }
                    ],
                    ResultFields =
                    [
                        new PluginResultFieldDesc { Key = "message", Name = "返回信息", FieldType = PluginFieldKind.Text },
                        new PluginResultFieldDesc { Key = "code", Name = "返回代码", FieldType = PluginFieldKind.Number },
                        new PluginResultFieldDesc { Key = "workflowId", Name = "流程ID", FieldType = PluginFieldKind.Text },
                        new PluginResultFieldDesc { Key = "echoBizNo", Name = "回显单号", FieldType = PluginFieldKind.Text },
                        new PluginResultFieldDesc { Key = "echoAmount", Name = "回显金额", FieldType = PluginFieldKind.Number },
                        new PluginResultFieldDesc { Key = "echoBizDate", Name = "回显日期", FieldType = PluginFieldKind.Timestamp },
                        new PluginResultFieldDesc { Key = "echoRemark", Name = "回显备注", FieldType = PluginFieldKind.TextArea },
                        new PluginResultFieldDesc { Key = "echoStatus", Name = "回显状态", FieldType = PluginFieldKind.SingleSelect },
                        new PluginResultFieldDesc { Key = "echoReceiver", Name = "回显经办人", FieldType = PluginFieldKind.SingleEmployee },
                        new PluginResultFieldDesc { Key = "echoDept", Name = "回显部门", FieldType = PluginFieldKind.SingleDepartment },
                        new PluginResultFieldDesc { Key = "echoAttachments", Name = "回显附件", FieldType = PluginFieldKind.FileUpload, Multiple = true },
                        new PluginResultFieldDesc { Key = "echoImages", Name = "回显图片", FieldType = PluginFieldKind.ImageUpload, Multiple = true },
                        new PluginResultFieldDesc { Key = "echoItems", Name = "回显明细", FieldType = PluginFieldKind.TableForm, Multiple = true }
                    ]
                },
                new PluginFunctionSnapshot
                {
                    Id = "EchoMixedData",
                    Name = "通用字段回显",
                    Description = "用于验证插件切换方法、字段重置和结果字段选择",
                    InputFields =
                    [
                        new PluginFieldDesc { Key = "title", Name = "标题", FieldType = PluginFieldKind.Text, Required = true },
                        new PluginFieldDesc { Key = "description", Name = "描述", FieldType = PluginFieldKind.TextArea },
                        new PluginFieldDesc { Key = "owner", Name = "负责人", FieldType = PluginFieldKind.SingleEmployee },
                        new PluginFieldDesc { Key = "ownerDept", Name = "归属部门", FieldType = PluginFieldKind.SingleDepartment }
                    ],
                    ResultFields =
                    [
                        new PluginResultFieldDesc { Key = "message", Name = "返回信息", FieldType = PluginFieldKind.Text },
                        new PluginResultFieldDesc { Key = "echoTitle", Name = "回显标题", FieldType = PluginFieldKind.Text },
                        new PluginResultFieldDesc { Key = "echoDescription", Name = "回显描述", FieldType = PluginFieldKind.TextArea },
                        new PluginResultFieldDesc { Key = "echoOwner", Name = "回显负责人", FieldType = PluginFieldKind.SingleEmployee },
                        new PluginResultFieldDesc { Key = "echoOwnerDept", Name = "回显归属部门", FieldType = PluginFieldKind.SingleDepartment }
                    ]
                }
            ]
        };
        await pluginProfileRepo.InsertAsync(profile);
    }
}
