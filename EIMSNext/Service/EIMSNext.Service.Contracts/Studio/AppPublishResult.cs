namespace EIMSNext.Service.Contracts
{
    /// <summary>
    /// 应用发布结果。
    /// </summary>
    /// <param name="TemplateId">应用模板 Id。</param>
    /// <param name="Warnings">发布过程中被跳过或保留原值的引用说明，供发布人员查看。</param>
    public sealed record AppPublishResult(string TemplateId, IReadOnlyList<string> Warnings);

    /// <summary>
    /// 应用发布 API 结果。
    /// </summary>
    /// <param name="ProfileId">应用档案 Id。</param>
    /// <param name="Warnings">发布过程中被跳过或保留原值的引用说明，供发布人员查看。</param>
    public sealed record AppPublishApiResult(string ProfileId, IReadOnlyList<string> Warnings);
}
