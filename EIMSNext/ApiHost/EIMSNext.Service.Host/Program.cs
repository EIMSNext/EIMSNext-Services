using Asp.Versioning;
using EIMSNext.ApiCore;
using EIMSNext.ApiCore.Plugin;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.Auth.Entities;
using EIMSNext.Async.RabbitMQ;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.ApiService;
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

builder.Services.AddControllers(options =>
{
    options.Filters.Add<IdentityTypeFilter>();
    options.Filters.Add<PermissionFilter>();
}).AddOData(options =>
{
    options.TimeZone = TimeZoneInfo.Utc;
    options.EnableQueryFeatures(EIMSNext.Common.Constants.MaxPageSize)
        .Conventions.Remove(options.Conventions.OfType<MetadataRoutingConvention>().First());

    options.RouteOptions.EnableControllerNameCaseInsensitive = true;
    options.RouteOptions.EnableActionNameCaseInsensitive = true;
    options.RouteOptions.EnablePropertyNameCaseInsensitive = true;
});

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
}).AddOData(opt => opt.AddRouteComponents("odata/v{version:apiVersion}", services =>
{
    services.AddSingleton<ODataEnumDeserializer, LowercaseODataEnumDeserializer>();
    services.AddSingleton<ODataEnumSerializer, LowercaseODataEnumSerializer>();
})).AddODataApiExplorer(opt =>
{
    opt.GroupNameFormat = "'v'VVV";
});

builder.Services.AddGlobalMef(EIMSNext.Common.Constants.BaseDirectory);
builder.Services.AddScoped<IPublicAccessValidator, PublicAccessValidator>();
builder.Services.AddPluginRuntime(EIMSNext.Common.Constants.BaseDirectory);
builder.Services.AddRabbitMqMessaging(builder.Configuration);

builder.Services.AddTransient<ISwaggerGenHandler, SwaggerGenHandler>();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, VersioningSwaggerGenOptions>();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
{
    await EnsureSeedData(serviceScope.ServiceProvider.GetRequiredService<IResolver>());
}

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
//app.UseHttpsRedirection();

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
        await corpService.AddAsync(new Corporate
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
            PublishedAt = DateTime.UtcNow
        };
        await pluginProfileRepo.InsertAsync(profile);
    }

    var corporateSettingService = resolver.GetService<CorporateSetting>();
    if (corporateSettingService != null && !corporateSettingService.All()
        .Any(x => x.CorpId == "test-corp" && x.Name == CorporateSettingNames.SsoSecret && !x.DeleteFlag))
    {
        await corporateSettingService.AddAsync(new CorporateSetting
        {
            CorpId = "test-corp",
            Name = CorporateSettingNames.SsoSecret,
            Value = string.Empty,
            Desc = "SSO Secret"
        });
    }
}
