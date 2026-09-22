using EIMSNext.Core.Extensions;
using EIMSNext.Entities;

namespace EIMSNext.Component
{
    /// <summary>
    /// <see cref="FormData"/> → 脚本引擎入参的构造扩展。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 依赖 <see cref="FormData"/> 实体，因此放在 Component 层（引用 Entities）：
    /// Flow.Core（工作流/事件流节点）与 Service（事件流调度）都要用它，而这两层都引用 Component。
    /// 另一个 <c>IDictionary</c> 版本的同名扩展在
    /// <see cref="DynamicDataExtensions.ToScriptData(IDictionary{string, object})"/>。
    /// </para>
    /// <para>
    /// <c>createBy</c> 会补进脚本入参（脚本表达式可以引用），但<b>不写回</b> <see cref="FormData.Data"/>：
    /// 原先 Flow.Core 的实现直接 <c>Data.TryAdd("createBy", ...)</c>，把审计对象塞进了业务
    /// 数据字典，会随 Data 一起落到 jsonb，并污染被 EF 跟踪的实体状态。这里改为在副本上补充，
    /// 与 Service 侧原本的实现保持一致（Service 侧一直就是构造副本）。
    /// </para>
    /// </remarks>
    public static class FormDataScriptDataExtensions
    {
        /// <summary>把表单数据包装成脚本引擎可识别的 <c>data.f_{formId}</c> 结构。</summary>
        /// <param name="formData">表单数据实体。</param>
        /// <returns>脚本入参。</returns>
        public static Dictionary<string, object> ToScriptData(this FormData formData)
        {
            var pData = new Dictionary<string, object?>(formData.Data);
            pData.TryAdd("createBy", formData.CreateBy);

            var wrapData = new Dictionary<string, object?>
            {
                [$"f_{formData.FormId}"] = pData,
            };

            return new Dictionary<string, object> { ["data"] = wrapData };
        }
    }
}
