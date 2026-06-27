namespace EIMSNext.Service.Host.Authorization
{
    /// <summary>
    /// 客户端授权所用的资源代码常量。
    ///
    /// 资源码与控制器 Action 上的 <c>[Permission(ResourceCode = ...)]</c> 一一对应；
    /// 客户端通过 <c>ClientGrant.ResourceActions</c> 获得若干资源-动作位掩码，
    /// 由 <c>PermissionFilter</c> 在每次请求时进行匹配。
    /// </summary>
    public static class Resources
    {
        /// <summary>成员（Employee）。</summary>
        public const string Employee = "employee";

        /// <summary>部门（Department）。</summary>
        public const string Department = "department";

        /// <summary>角色（Role）。</summary>
        public const string Role = "role";

        /// <summary>角色组（RoleGroup）。</summary>
        public const string RoleGroup = "roleGroup";

        /// <summary>应用（AppDef）。仅读。</summary>
        public const string AppDef = "appdef";

        /// <summary>表单（FormDef）。仅读。</summary>
        public const string FormDef = "formdef";

        /// <summary>表单数据（FormData）。</summary>
        public const string FormData = "formdata";

        /// <summary>工作流实例（WorkflowInstance）。</summary>
        public const string WorkflowInstance = "workflow.instance";

        /// <summary>工作流任务（WorkflowTask）。</summary>
        public const string WorkflowTask = "workflow.task";

        /// <summary>全部资源码。用于 ClientGrant.ApiScope="all" 时的码展开。</summary>
        public static readonly string[] All = new[]
        {
            Employee,
            Department,
            Role,
            RoleGroup,
            AppDef,
            FormDef,
            FormData,
            WorkflowInstance,
            WorkflowTask,
        };
    }
}
