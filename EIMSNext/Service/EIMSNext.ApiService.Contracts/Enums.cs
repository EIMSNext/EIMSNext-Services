namespace EIMSNext.ApiService
{
    [Flags]
    public enum IdentityType
    {
        /// <summary>
        /// 无身份用户
        /// </summary>
        None = 0,

        /// <summary>
        /// 系统
        /// </summary>
        System = 1,

        /// <summary>
        /// 客户端
        /// </summary>
        Client = 2,

        /// <summary>
        /// 企业创建者
        /// </summary>
        CorpOwmer = 4,

        /// <summary>
        /// 超级管理员
        /// </summary>
        CorpAdmin = 8,

        /// <summary>
        /// 企业创建者或超级管理员
        /// </summary>
        Corp_Admins = 12,

        /// <summary>
        /// 普通管理员
        /// </summary>
        AppAdmin = 16,

        /// <summary>
        /// 普通管理员或企业创建者或超级管理员
        /// </summary>
        App_Admins = 28,

        /// <summary>
        /// 应用管理员
        /// </summary>
        FormAdmin = 32,

        /// <summary>
        /// 应用管理员或普通管理员或企业创建者或超级管理员
        /// </summary>
        Form_Admins = 60,

        /// <summary>
        /// 普通员工
        /// </summary>
        Employee = 64,

        /// <summary>
        /// 普通员工或应用管理员或普通管理员或企业创建者或超级管理员
        /// </summary>
        Employee_Admins = 124,

        /// <summary>
        /// 无企业用户
        /// </summary>
        NoCorp = 248,

        /// <summary>
        /// 匿名用户
        /// </summary>
        Anonymous = 32768,

        /// <summary>
        /// 用户已被禁用
        /// </summary>
        Disabled = 65536,
    }

    public enum AccessControlLevel
    {
        /// <summary>
        /// 根据权限控制
        /// </summary>
        NotSet = 0,
        /// <summary>
        /// 总是允许
        /// </summary>
        Allow = 1,
        /// <summary>
        /// 总是禁止
        /// </summary>
        Forbid = 2,
        /// <summary>
        /// 资源所有者
        /// </summary>
        Owner = 3
    }
}
