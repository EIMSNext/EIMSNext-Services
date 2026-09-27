using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 表单数据视图模型。
    /// </summary>
    public class FormDataViewModel : FormData
    {
        /// <summary>数据标题。</summary>
        public string? DataTitle { get; set; }

        /// <summary>
        /// 从表单数据创建视图模型，去除不希望返回的属性。
        /// </summary>
        /// <param name="formData">表单数据。</param>
        /// <param name="dataTitle">数据标题。</param>
        /// <returns>表单数据视图模型。</returns>
        public static FormDataViewModel FromFormData(FormData formData, string? dataTitle = null)
        {
            //通过转换，去掉一些不想返回的属性
            FormDataViewModel form = formData.CastTo<FormData, FormDataViewModel>();

            form.CorpId = null;
            form.UpdateBy = null;
            form.DataTitle = dataTitle;

            return form;
        }
    }
}
