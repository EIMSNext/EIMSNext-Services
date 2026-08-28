using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 可计费能力的目标类别。
    /// </summary>
    public enum ECoinTargetType
    {
        /// <summary>
        /// 短信能力。
        /// </summary>
        SMS,

        /// <summary>
        /// 电子邮件能力。
        /// </summary>
        EMail,

        /// <summary>
        /// 插件能力。
        /// </summary>
        Plugin
    }

    /// <summary>
    /// 可计费能力的计费方式。
    /// </summary>
    public enum ECoinChargeType
    {
        /// <summary>
        /// 按 E 币余额计费。
        /// </summary>
        ECoin,

        /// <summary>
        /// 按订阅计费。
        /// </summary>
        Subscription
    }

    /// <summary>
    /// 平台能力的统一定价。
    /// </summary>
    public class ECoinPrice : MongoEntityBase
    {
        /// <summary>
        /// 被定价能力的目标类别。
        /// </summary>
        public ECoinTargetType TargetType { get; set; }

        /// <summary>
        /// 能力的稳定标识。
        /// </summary>
        public string FeatureId { get; set; } = string.Empty;

        /// <summary>
        /// 能力的显示说明。
        /// </summary>
        public string FeatureDesc { get; set; } = string.Empty;

        /// <summary>
        /// 单位价格。
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 应用此价格的计费方式。
        /// </summary>
        public ECoinChargeType ChargeType { get; set; }

        /// <summary>
        /// 目标为插件时对应的插件标识；其他目标类型为空字符串。
        /// </summary>
        public string PluginId { get; set; } = string.Empty;
    }
}
