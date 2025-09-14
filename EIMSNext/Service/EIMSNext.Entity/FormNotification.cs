using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 表单通知
    /// </summary>
    public class FormNotification : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string FormId {  get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string Name {  get; set; } = "";
    }
}
