using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 表单公开发布请求
    /// </summary>
    public class PublicSettingRequest : RequestBase
    {
        /// <summary>
        /// 所属应用 ID。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 公开资源类型。
        /// </summary>
        public PublicTargetType TargetType { get; set; } = PublicTargetType.Form;

        /// <summary>
        /// 公开资源 ID。
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>
        /// 表单公开设置。
        /// </summary>
        public PublicFormSetting Form { get; set; } = new();

        /// <summary>
        /// 仪表盘公开设置。
        /// </summary>
        public PublicDashboardSetting Dashboard { get; set; } = new();
    }
}
