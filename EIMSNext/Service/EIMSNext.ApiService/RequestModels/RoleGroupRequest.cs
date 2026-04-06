namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 角色组请求
    /// </summary>
    public class RoleGroupRequest : RequestBase
    {
        /// <summary>
        /// 角色组名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 角色组描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 排序值
        /// </summary>
        public int SortValue { get; set; }
    }
}
