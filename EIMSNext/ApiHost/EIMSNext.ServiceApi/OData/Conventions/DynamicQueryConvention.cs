using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Routing;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.AspNetCore.OData.Routing.Template;

namespace EIMSNext.ServiceApi.OData.Conventions
{
    public class DynamicQueryConvention : IODataControllerActionConvention
    {
        public int Order => 0;

        public bool AppliesToController(ODataControllerActionContext context)
        {
            return context.Controller.ControllerType.IsGenericType &&
                context.Controller.ControllerType.GetGenericTypeDefinition() == typeof(ReadOnlyODataController<,>);
        }

        public bool AppliesToAction(ODataControllerActionContext context)
        {
            if (AppliesToController(context))
            {
                var dynamicQueryAction = context.Controller.Actions.FirstOrDefault(action =>
                    action.ActionMethod.GetCustomAttributes(typeof(DynamicQueryAttribute), false).Any());

                if (dynamicQueryAction != null)
                {
                    // 创建自定义路由模板
                    var template = new ODataPathTemplate(new DynamicQuerySegmentTemplate());

                    // 添加选择器
                    dynamicQueryAction.AddSelector("POST", context.Prefix, context.Model, template);
                    return true;
                }
            }
            return false;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class DynamicQueryAttribute : Attribute
    {
    }
    public class DynamicQuerySegmentTemplate : ODataSegmentTemplate
    {
        public override IEnumerable<string> GetTemplates(ODataRouteOptions options)
        {
            yield return "/$dynamicquery";
        }

        public override bool TryTranslate(ODataTemplateTranslateContext context)
        {
            return true;
        }
    }
}
