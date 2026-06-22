using EIMSNext.ApiService;

namespace EIMSNext.Service.Host.Authorization
{
    /// <summary>
    /// 标记公开访问 action 需要的 scope。
    /// 仅当请求 token 的 PublicScope 包含此值时允许访问。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class PublicScopeAttribute : Attribute
    {
        public PublicScope Scope { get; }

        public PublicScopeAttribute(PublicScope scope)
        {
            Scope = scope;
        }
    }
}
