using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    public class WfTaskViewModel : Wf_Task
    {
        /// <summary>
        /// 关联表单名称
        /// </summary>
        public string? FormName {  get; set; }
    }
}

