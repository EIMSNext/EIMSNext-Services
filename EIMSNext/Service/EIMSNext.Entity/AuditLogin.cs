using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 登录日志
    /// </summary>
    public class AuditLogin : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string UserId { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string EmpId { get; set; } = "";
    }
}
