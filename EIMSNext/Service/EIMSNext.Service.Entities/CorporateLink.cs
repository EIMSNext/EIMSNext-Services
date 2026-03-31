using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 
    /// </summary>
    public class CorporateLink : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string LinkCorpId { get; set; } = string.Empty;
    }
}
