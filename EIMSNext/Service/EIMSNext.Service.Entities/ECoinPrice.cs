using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    public enum ECoinTargetType
    {
        SMS,
        EMail,
        Plugin
    }

    public enum ECoinChargeType
    {
        ECoin,
        Subscription
    }

    /// <summary>平台能力的统一定价。</summary>
    public class ECoinPrice : MongoEntityBase
    {
        public ECoinTargetType TargetType { get; set; }
        public string FeatureId { get; set; } = string.Empty;
        public string FeatureDesc { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public ECoinChargeType ChargeType { get; set; }
        public string PluginId { get; set; } = string.Empty;
    }
}
