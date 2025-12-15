using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Routing;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.AspNetCore.OData.Routing.Template;

namespace EIMSNext.ServiceApi.OData.Conventions
{
    public class PatchSetConvention : IODataControllerActionConvention
    {
        public int Order => 0;

        public bool AppliesToController(ODataControllerActionContext context)
        {
            return context.Controller.ControllerType.IsGenericType &&
                context.Controller.ControllerType.GetGenericTypeDefinition() == typeof(ODataController<,,>);
        }

        public bool AppliesToAction(ODataControllerActionContext context)
        {
            if (AppliesToController(context))
            {
                var patchSetAction = context.Controller.Actions.FirstOrDefault(action =>
                    action.ActionMethod.Name.Equals("PatchSet", StringComparison.OrdinalIgnoreCase));

                if (patchSetAction != null)
                {
                    // 创建自定义路由模板
                    var template = new ODataPathTemplate(new EntitySetSegmentTemplate(context.EntitySet));

                    // 添加选择器
                    patchSetAction.AddSelector("PATCH", context.Prefix, context.Model, template);
                    return true;
                }
            }
            return false;
        }
    }

    public class PatchSetSegmentTemplate : ODataSegmentTemplate
    {
        public override IEnumerable<string> GetTemplates(ODataRouteOptions options)
        {
            yield return "/PatchSet";
        }

        public override bool TryTranslate(ODataTemplateTranslateContext context)
        {
            return true;
        }
    }
}
