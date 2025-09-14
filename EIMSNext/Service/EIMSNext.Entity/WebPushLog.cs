using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// Web推送日志
    /// </summary>
    public class WebPushLog : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string AppId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string FormId { get; set; } = "";
    }
}
