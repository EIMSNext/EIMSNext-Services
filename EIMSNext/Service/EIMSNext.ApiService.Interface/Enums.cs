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
        /// 普通管理员
        /// </summary>
        AppAdmin = 16,

        /// <summary>
        /// 应用管理员
        /// </summary>
        FormAdmin = 32,

        /// <summary>
        /// 普通员工
        /// </summary>
        Employee = 64,

        /// <summary>
        /// 匿名用户
        /// </summary>
        Anonymous = 32768,

        /// <summary>
        /// 用户已被禁用
        /// </summary>
        Disabled = 65536,
    }

    public static class IdentityTypes
    {
        public static IdentityType CorpAdminAndOwners => IdentityType.CorpOwmer | IdentityType.CorpOwmer;
        public static IdentityType AppAdminAndCorpAdmins => CorpAdminAndOwners | IdentityType.AppAdmin;
        public static IdentityType FormAdminAndAppAdmins => AppAdminAndCorpAdmins | IdentityType.FormAdmin;
        public static IdentityType EmployeeAndAdmins => FormAdminAndAppAdmins | IdentityType.Employee;
    }

    //public enum AccessControlLevel
    //{
    //    / <summary>
    //    / 根据权限控制
    //    / </summary>
    //    NotSet = 0,
    //    / <summary>
    //    / 总是允许
    //    / </summary>
    //    Allow = 1,
    //    / <summary>
    //    / 总是禁止
    //    / </summary>
    //    Forbid = 2
    //}
}
