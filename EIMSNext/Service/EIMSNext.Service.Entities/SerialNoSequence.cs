using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 流水号生成器
    /// </summary>
    public class SerialNoSequence : CorpEntityBase, IEntity
    {
        /// <summary>
        /// 应用ID，用于标识流水号所属的应用
        /// </summary>
        public string AppId { get; set; } = string.Empty;
        /// <summary>
        /// 表单ID，用于标识流水号所属的表单
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 当前日期，用于按天重置流水号
        /// </summary>
        public DateTime? CurrDate { get; set; }

        /// <summary>
        /// 当前流水号值，记录最新的序列号
        /// </summary>
        public int? CurrId { get; set; }

        /// <summary>
        /// 流水号类型
        /// </summary>
        public SerialNoType SerialNoType { get; set; }
    }

    /// <summary>
    /// 流水号类型枚举，定义流水号的作用范围
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
        /// 初始化生成流水号的参数
        /// </summary>
        /// <param name="serialNoType">流水号类型</param>
        /// <param name="platform">注册平台</param>
        /// <param name="corpId">企业Id</param>
        /// <param name="appId">应用Id</param>
        /// <param name="formId">表单Id</param>
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
