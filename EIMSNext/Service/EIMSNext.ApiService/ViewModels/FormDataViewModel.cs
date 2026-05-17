using EIMSNext.Common;
using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    public class FormDataViewModel : FormData
    {
        public string? DataTitle { get; set; }

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
