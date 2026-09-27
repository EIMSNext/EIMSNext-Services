using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 工作流任务视图模型。
    /// </summary>
    public class WfTaskViewModel : Wf_Task
    {
        /// <summary>
        /// 关联表单名称
        /// </summary>
        public string? FormName {  get; set; }
    }
}

