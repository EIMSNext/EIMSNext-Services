using System.ComponentModel.DataAnnotations.Schema;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

namespace EIMSNext.Entities
{
    public class User : KeyedEntityBase, IUser
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

        /// <summary>
        /// 用户与企业的归属关系（源自关系表 "UserCorp"）。
        /// 不参与持久化映射，由宿主按需查询后填充，取代原先内嵌的 jsonb 投影字段。
        /// </summary>
        [NotMapped]
        public List<UserCorp> UserCorps { get; set; } = new List<UserCorp>();

        public bool IsSystem => Id == "system";
        public bool IsAnonymous => Id == "anonymous";
    }

    /// <summary>
    /// 用户与企业的关系（独立表 "UserCorp"），是唯一事实来源。
    /// 通过 <see cref="UserId"/> 关联所属用户。
    /// 实现 <see cref="IEntityKey"/> 以便经 <c>IRepository&lt;UserCorp&gt;</c> 读写。
    /// </summary>
    public class UserCorp : IEntityKey
    {
        /// <summary>
        /// 关系主键。Id 项目内统一保持字符串契约，不使用数据库自增。
        /// </summary>
        public string Id { get; set; } = "";
        /// <summary>
        /// 所属用户 ID。
        /// </summary>
        public string UserId { get; set; } = "";
        /// <summary>
        /// 企业ID。与 Corporate.Id 同为字符串契约，避免与业务标识混用整型。
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
