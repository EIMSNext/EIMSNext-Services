using Asp.Versioning.ApiExplorer;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace EIMSNext.ApiHost.Extension
{
    /// <summary>
    /// 
    /// </summary>
    public class VersioningSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
    {
        private IApiVersionDescriptionProvider provider;
        private ISwaggerGenHandler handler;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="provider"></param>
        public VersioningSwaggerGenOptions(IApiVersionDescriptionProvider provider, ISwaggerGenHandler handler)
        {
            this.provider = provider;
            this.handler = handler;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        public void Configure(SwaggerGenOptions options)
        {
            //options.IncludeXmlComments("D:\\Fork\\doc\\api.xml");
            //options.IncludeXmlComments("D:\\Fork\\doc\\entity.xml");
            this.handler.IncludeXmlComments();

            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, this.handler.CreateOpenApiInfo(description));
            }

            options.DocInclusionPredicate((version, apiDescription) =>
            {
                if (version != apiDescription.GroupName)
                    return false;

                var values = apiDescription.RelativePath?.Split('/').Select(v => v.Replace("v{version}", apiDescription.GroupName));
                if (values != null) apiDescription.RelativePath = string.Join("/", values);
                return true;
            });

            options.TagActionsBy(apiDescription =>
            {
                var actionDesc = (ControllerActionDescriptor)apiDescription.ActionDescriptor;
                return new string[] {
                    ((actionDesc.ControllerTypeInfo.Namespace??string.Empty).Contains("ODataControllers")?"OData 接口":"API 接口") + " - " + actionDesc.ControllerName};
            });

            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Scheme = "bearer",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    },
                    new List<string>()
                },
            });
        }
    }
}
