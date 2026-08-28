using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Entities
{
    public class User : MongoEntityBase, IUser
    {
        /// <summary>
        /// 注册时间（Unix 毫秒时间戳）
        /// </summary>
        public long CreateTime { get; set; }

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

        /// <summary>
        /// 头像文件的相对存储路径
        /// </summary>
        public string? Avatar { get; set; }

        /// <summary>
        /// 显式用户身份。为空时由业务服务按企业和员工关系计算。
        /// </summary>
        public string? UserType { get; set; }

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
