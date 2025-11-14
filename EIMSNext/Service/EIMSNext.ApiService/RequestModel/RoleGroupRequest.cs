namespace EIMSNext.ApiService.RequestModel
{
    public class RoleGroupRequest : RequestBase
    {
        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 角色描述
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public int SortValue { get; set; }
    }
}

