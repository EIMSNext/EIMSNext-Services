namespace EIMSNext.ApiService.RequestModel
{
    public class RoleRequest : RequestBase
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
        public string RoleGroupId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public int SortValue { get; set; }
    }

    public class AddEmpsToRoleRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public required string RoleId {  get; set; }
      /// <summary>
      /// 
      /// </summary>
        public required string EmpIds { get; set; }
    }
}

