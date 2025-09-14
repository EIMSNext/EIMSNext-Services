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

            form.ApprovalLogs.ForEach(log =>
            {
                if (log.Approver != null)
                {
                    log.Approver.CorpId = null; 
                    log.Approver.UserId = null;
                }
            });

            return form;
        }
    }
}
