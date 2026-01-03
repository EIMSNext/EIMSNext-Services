using EIMSNext.Entity;

namespace EIMSNext.ApiService.RequestModel
{
    public class AuthGroupRequest : RequestBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public string Desc { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public AuthGroupType Type { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<Member> Members { get; set; } = new List<Member>();
        /// <summary>
        /// 
        /// </summary>
        public DataPerms DataPerms { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string? DataFilter { get; set; }
        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool Disabled { get; set; }
    }
}

