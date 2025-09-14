using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using EIMSNext.Cache;
using EIMSNext.Common.Extension;
using EIMSNext.Common.Serialization;
using EIMSNext.Core.Serialization;
using EIMSNext.MongoDb;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

using StackExchange.Redis;

using ISessionStore = EIMSNext.Cache.ISessionStore;

namespace EIMSNext.ApiCore
{
    public static class ServiceCollectionExtensions
    {
        public static void ConfigCommonServices(this IHostApplicationBuilder builder)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            EIMSNext.Common.Constants.BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            EIMSNext.Common.Constants.ContentRootPath = builder.Environment.ContentRootPath;
            if (builder.Environment is IWebHostEnvironment)
            {
                EIMSNext.Common.Constants.WebRootPath = (builder.Environment as IWebHostEnvironment)!.WebRootPath;
            }

            builder.Services.Configure<MongoDbConfiguration>(builder.Configuration.GetSection("MongoDb"));

            builder.Services.Configure<JsonOptions>(opt =>
            {
                opt.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                opt.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
                opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                opt.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                opt.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

                opt.JsonSerializerOptions.Converters.Add(new BsonDocumentJsonConverter());
                opt.JsonSerializerOptions.Converters.Add(new ExceptionJsonConverter());
                opt.JsonSerializerOptions.Converters.Add(new FlexibleEnumConverterFactory());
                opt.JsonSerializerOptions.Converters.Add(new ObjectJsonConverter());
                opt.JsonSerializerOptions.Converters.Add(new ExpandoObjectJsonConverter());
                //opt.JsonSerializerOptions.Converters.Add(new UnixMillisecondsDateTimeJsonConverter());

                JsonSerializerExtension.SetOptions(opt.JsonSerializerOptions);
            });

            //将Redis分布式缓存服务添加到服务中
            builder.AddCustomCache();

            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            builder.AddCustomAuthentication();
        }

        public static void AddCustomCache(this IHostApplicationBuilder builder)
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                //Redis实例名
                options.InstanceName = "RedisDistributedCache";
                options.ConfigurationOptions = new ConfigurationOptions()
                {
                    // User
                    //Password = "xxxxxx",
                    //AllowAdmin = true,
                    DefaultDatabase = (builder.Configuration.GetSection("CacheServer:Database").Value ?? "1").SafeToInt(1),
                    AbortOnConnectFail = false,//当为true时，当没有可用的服务器时则不会创建一个连接
                };
                options.ConfigurationOptions.EndPoints.Add(builder.Configuration.GetSection("CacheServer:EndPoint").Value ?? "localhost:6379");
            });

            builder.Services.AddSingleton<ICacheClient, DistributedCacheClient>();

            builder.Services.AddScoped<ISessionStore, SessionStore>();
        }

        public static void AddCustomAuthentication(this IHostApplicationBuilder builder)
        {
            builder.Services.AddAuthentication(o =>
            {
                o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(JwtBearerDefaults.AuthenticationScheme,
             opt =>
             {
                 opt.Authority = builder.Configuration.GetSection("OAuth:Authority").Value;
                 opt.SaveToken = true;
                 opt.RequireHttpsMetadata = false;

                 opt.TokenValidationParameters = new TokenValidationParameters
                 {
                     ValidateIssuer = true,
                     ValidIssuer = "https://auth.eimsnext.com",
                     ValidateAudience = true,
                     ValidAudience = "eimsnext.api",
                     ValidateLifetime = true,
                     ValidateIssuerSigningKey = true
                 };
             });
        }
        public static void UseCustomMiddlewares(this IApplicationBuilder app)
        {
            app.UseMiddleware<ExceptionFilterMiddleware>();
            app.UseMiddleware<CorsMiddleware>();
        }
    }
}
