using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 流水号生成器
    /// </summary>
    public class SerialNoSequence : CorpEntityBase, IEntity
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
        public DateTime? CurrDate { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int? CurrId { get; set; }

        /// <summary>
        /// 流水号类型
        /// </summary>
        public SerialNoType SerialNoType { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum SerialNoType
    {
        /// <summary>
        /// 企业
        /// </summary>
        Corporate = 1,
        /// <summary>
        /// 表单
        /// </summary>
        Form
    }

    /// <summary>
    /// 生成批次的参数对象
    /// </summary>
    public class NextSerialNoParameter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialNoType"></param>
        /// <param name="platform"></param>
        /// <param name="corpId"></param>
        /// <param name="appId"></param>
        /// <param name="formId"></param>
        public NextSerialNoParameter(SerialNoType serialNoType, PlatformType platform, string corpId, string appId, string formId)
        {
            SerialNoType = serialNoType;
            Platform = platform;
            CorpId = corpId;
            AppId = appId;
            FormId = formId;
        }

        /// <summary>
        /// 单据类型
        /// </summary>
        public SerialNoType SerialNoType { get; private set; }
        /// <summary>
        /// 注册平台
        /// </summary>
        public PlatformType Platform { get; private set; }
        /// <summary>
        /// 企业Id
        /// </summary>
        public string CorpId { get; private set; }
        /// <summary>
        /// 应用Id
        /// </summary>
        public string AppId { get; set; }
        /// <summary>
        /// 表单Id
        /// </summary>
        public string FormId { get; private set; }
    }
}
