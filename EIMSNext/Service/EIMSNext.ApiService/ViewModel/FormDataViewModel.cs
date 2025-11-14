using EIMSNext.Common;
using EIMSNext.Entity;

namespace EIMSNext.ApiService.ViewModel
{
    public class FormDataViewModel : FormData
    {
        public static FormDataViewModel FromFormData(FormData formData)
        {
            //通过转换，去掉一些不想返回的属性
            FormDataViewModel form = formData.CastTo<FormData, FormDataViewModel>();

            form.CorpId = null;

            if (form.CreateBy != null)
            {
                form.CreateBy.CorpId = null;
                form.CreateBy.UserId = null;
            }

            form.UpdateBy = null;                      

            return form;
        }
    }
}
