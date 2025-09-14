using EIMSNext.Core.Entity;

namespace EIMSNext.Auth.Entity
{
    public class User : MongoEntityBase, IUser
    {
        /// <summary>
        /// 昵称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email { get; set; } = "";
        /// <summary>
        /// 电话
        /// </summary>
        public string Phone { get; set; } = "";
        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; } = string.Empty;
        /// <summary>
        /// 注册来源
        /// </summary>
        public PlatformType Platform { get; set; }
        /// <summary>
        /// 已禁用/锁定
        /// </summary>
        public bool Disabled {  get; set; }

        public IList<UserCorp> Crops { get; set; } = new List<UserCorp>();

        public bool IsSystem => Id == "system";
        public bool IsAnonymous => Id == "anonymous";
    }

    public class UserCorp
    {
        /// <summary>
        /// 企业ID
        /// </summary>
        public string CorpId { get; set; } = "";
        /// <summary>
        /// 是否企业所有者
        /// </summary>
        public bool IsCorpOwner { get; set; }
        /// <summary>
        /// 内部企业/互联企业
        /// </summary>
        public string CorpType { get; set; } = "";
        /// <summary>
        /// 是否当前登录企业
        /// </summary>
        public bool IsDefault { get; set; }
    }
}
