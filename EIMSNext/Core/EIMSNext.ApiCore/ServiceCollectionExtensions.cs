using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using EIMSNext.Cache;
using EIMSNext.Core;
using EIMSNext.Core.Serialization;
using EIMSNext.Json.Serialization;
using EIMSNext.MongoDb;
using EIMSNext.Storage;
using EIMSNext.Storage.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using ISessionStore = EIMSNext.Cache.ISessionStore;

namespace EIMSNext.ApiCore
{
    public static class ServiceCollectionExtensions
    {
        public static void ConfigWebEnvironment(this IHostApplicationBuilder builder)
        {
            EIMSNext.Common.Constants.ContentRootPath = builder.Environment.ContentRootPath;

            if (builder.Environment is IWebHostEnvironment)
            {
                EIMSNext.Common.Constants.WebRootPath = (builder.Environment as IWebHostEnvironment)!.WebRootPath;
            }

            builder.Services.AddBasicServices(builder.Configuration);
            builder.Services.AddCustomCache(builder.Configuration);
            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            builder.Services.AddCustomAuthentication(builder.Configuration);
        }

        public static void AddBasicServices(this IServiceCollection services, IConfiguration configuration)
        {
            MongoDatabase.RegisterConventions();
            MongoDatabase.RegisterSerializers();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            EIMSNext.Common.Constants.BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            services.Configure<MongoDbConfiguration>(configuration.GetSection("MongoDb"));
            services.Configure<StorageConfiguration>(configuration.GetSection("Storage"));

            services.Configure<JsonOptions>(opt =>
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

            services.AddSingleton<IStorageProvider, LocalStorageProvider>();
        }
        public static void AddCustomCache(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                //Redis实例名
                options.InstanceName = "RedisDistributedCache";
                options.ConfigurationOptions = new ConfigurationOptions()
                {
                    // User
                    //Password = "xxxxxx",
                    //AllowAdmin = true,
                    DefaultDatabase = (configuration.GetSection("CacheServer:Database").Value ?? "1").SafeToInt(1),
                    AbortOnConnectFail = false,//当为true时，当没有可用的服务器时则不会创建一个连接
                };
                options.ConfigurationOptions.EndPoints.Add(configuration.GetSection("CacheServer:EndPoint").Value ?? "localhost:6379");
            });

            services.AddSingleton<ICacheClient, DistributedCacheClient>();
            services.AddScoped<ISessionStore, SessionStore>();
        }

        public static void AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(o =>
            {
                o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(JwtBearerDefaults.AuthenticationScheme,
             opt =>
             {
                 opt.Authority = configuration.GetSection("OAuth:Authority").Value;
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
