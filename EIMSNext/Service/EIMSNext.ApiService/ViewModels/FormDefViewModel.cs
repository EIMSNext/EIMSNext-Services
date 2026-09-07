using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 表单定义视图模型。
    /// </summary>
    public class FormDefViewModel : FormDef
    {
        /// <summary>
        /// 获取或设置External。
        /// </summary>
        public bool External { get; set; }
    }
}

