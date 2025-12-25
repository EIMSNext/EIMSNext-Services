using System.Text;

using Asp.Versioning.ApiExplorer;

using Microsoft.OpenApi.Models;

namespace EIMSNext.ApiHost.Extension
{
    public interface ISwaggerGenHandler
    {
        abstract string Title { get; }
        void IncludeXmlComments();
        OpenApiInfo CreateOpenApiInfo(ApiVersionDescription description);
    }
    public abstract class SwaggerGenHandlerBase : ISwaggerGenHandler
    {
        public abstract string Title { get; }

        public virtual void IncludeXmlComments()
        { }

        public OpenApiInfo CreateOpenApiInfo(ApiVersionDescription description)
        {
            var text = new StringBuilder();
            var info = new OpenApiInfo()
            {
                Title = $"{Title} - V{description.ApiVersion}",
                Version = description.ApiVersion.ToString(),
                Contact = new OpenApiContact() { Name = "EIMSNext Team", Email = "dev@eimsnext.com" },
            };

            if (description.IsDeprecated)
            {
                text.Append(" This API version has been deprecated.");
            }

            //text.Append("<h4>Additional Information</h4>");
            info.Description = text.ToString();

            return info;
        }
    }
}
