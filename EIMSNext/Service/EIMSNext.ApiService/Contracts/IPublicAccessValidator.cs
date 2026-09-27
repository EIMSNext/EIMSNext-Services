using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Entities;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 公开访问校验器接口，用于校验公开链接的访问权限并施加数据范围。
    /// </summary>
    public interface IPublicAccessValidator
    {
        /// <summary>
        /// 获取一个值，指示当前身份是否为公开身份。
        /// </summary>
        bool IsPublicIdentity { get; }

        /// <summary>
        /// 获取目标 ID。
        /// </summary>
        string TargetId { get; }

        /// <summary>
        /// 获取当前公开设置。
        /// </summary>
        /// <returns>公开设置，未配置时为 null。</returns>
        PublicSetting? GetCurrentSetting();

        /// <summary>
        /// 判断是否启用了任意公开分区。
        /// </summary>
        /// <returns>已启用时为 true，否则为 false。</returns>
        bool IsAnySectionEnabled();

        /// <summary>
        /// 判断是否可以读取指定仪表盘。
        /// </summary>
        /// <param name="dashboardId">仪表盘 ID。</param>
        /// <returns>可读取时为 true。</returns>
        bool CanReadDashboard(string dashboardId);

        /// <summary>
        /// 判断是否可以读取指定仪表盘项。
        /// </summary>
        /// <param name="itemId">仪表盘项 ID。</param>
        /// <returns>可读取时为 true。</returns>
        bool CanReadDashboardItem(string itemId);

        /// <summary>
        /// 判断是否可以读取指定表单定义。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        /// <returns>可读取时为 true。</returns>
        bool CanReadFormDefinition(string formId);

        /// <summary>
        /// 判断是否可以提交指定表单。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        /// <returns>可提交时为 true。</returns>
        bool CanSubmitForm(string formId);

        /// <summary>
        /// 判断是否可以读取指定表单数据。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        /// <returns>可读取时为 true。</returns>
        bool CanReadFormData(string formId);

        /// <summary>
        /// 判断是否可以查询指定表单数据。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        /// <returns>可查询时为 true。</returns>
        bool CanQueryFormData(string formId);

        /// <summary>
        /// 判断指定表单是否为关联表单。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        /// <returns>是关联表单时为 true。</returns>
        bool IsRelatedForm(string formId);

        /// <summary>
        /// 判断是否可以读取仪表盘关联的表单。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        /// <returns>可读取时为 true。</returns>
        bool CanReadDashboardForm(string formId);

        /// <summary>
        /// 获取可读的表单 ID 集合。
        /// </summary>
        /// <returns>可读表单 ID 集合。</returns>
        IReadOnlyCollection<string> GetReadableFormIds();

        /// <summary>
        /// 为表单数据施加公开访问的数据范围过滤。
        /// </summary>
        /// <param name="formId">表单 ID。</param>
        /// <param name="filter">原始过滤条件。</param>
        /// <returns>施加范围后的过滤条件。</returns>
        DynamicFilter ApplyFormDataScope(string formId, DynamicFilter? filter);
    }
}
